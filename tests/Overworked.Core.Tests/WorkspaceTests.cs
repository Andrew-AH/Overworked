using Overworked.Core;

namespace Overworked.Core.Tests;

/// <summary>Integration tests: real git, against throwaway repos with a bare "origin" each.</summary>
public sealed class WorkspaceTests : IDisposable
{
    readonly string _dir = RealPath(Directory.CreateTempSubdirectory("overworked-").FullName);

    // macOS's temp folder is under /var, a link to /private/var, and git reports worktree folders by their real path.
    static string RealPath(string path) => OperatingSystem.IsMacOS() && path.StartsWith("/var/") ? "/private" + path : path;

    [Fact]
    public void Creates_and_removes_a_worktree_under_root()
    {
        var root = NewRoot();
        Directory.CreateDirectory(Path.Combine(root, "not-a-repo"));
        var app = CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);

        var repo = Assert.Single(workspace.LoadRepos());
        var created = workspace.CreateWorktree(repo, "feature/PROJ-1");

        Assert.Equal(Path.Combine(root, ".worktrees", "app", "feature-PROJ-1"), created.Path);
        repo = Assert.Single(workspace.LoadRepos()); // the worktree itself is not picked up as a repo
        Assert.Equal(new[] { "main", "feature/PROJ-1" }, repo.Worktrees.Select(w => w.Branch));
        // A brand-new branch with exactly that name, cut from origin/main, not tracking it.
        Assert.Equal("feature/PROJ-1", Git.Run(created.Path, "branch", "--show-current").Trim());
        Assert.Equal(Git.Run(app, "rev-parse", "origin/main"), Git.Run(app, "rev-parse", "feature/PROJ-1"));
        Assert.False(Git.Succeeds(app, "rev-parse", "--abbrev-ref", "feature/PROJ-1@{upstream}"));

        File.WriteAllText(Path.Combine(created.Path, "wip.txt"), "x");
        Assert.Equal(1, workspace.LoadRepo("app").Worktrees[1].ChangedFiles);
        Assert.Throws<OverworkedException>(() => workspace.RemoveWorktree(repo, repo.Worktrees[1], force: false));
        Assert.True(Directory.Exists(created.Path));

        workspace.RemoveWorktree(repo, repo.Worktrees[1], force: true);

        Assert.Single(workspace.LoadRepo("app").Worktrees);
        Assert.False(Directory.Exists(Path.Combine(root, ".worktrees")));
        Assert.True(Git.Succeeds(app, "show-ref", "--verify", "--quiet", "refs/heads/feature/PROJ-1")); // branch kept
    }

    [Fact]
    public void Recreating_a_removed_worktree_reuses_its_branch()
    {
        var root = NewRoot();
        CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);
        var repo = workspace.LoadRepos()[0];

        workspace.RemoveWorktree(repo, workspace.CreateWorktree(repo, "PROJ-2"), force: false);
        var again = workspace.CreateWorktree(repo, "PROJ-2");

        Assert.True(Directory.Exists(again.Path));
    }

    [Fact]
    public void Refuses_to_remove_a_worktree_whose_submodule_has_unpushed_commits()
    {
        // Local-path submodule URLs are blocked by default since git 2.38.
        Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", "protocol.file.allow");
        Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", "always");
        try
        {
            var root = NewRoot();
            CloneWithOneCommit(root, "infra");
            var app = CloneWithOneCommit(root, "app");
            Git.Run(app, "submodule", "add", Path.Combine(_dir, "infra.git"), "infra");
            Commit(app, "add infra");
            Git.Run(app, "push", "origin", "main");

            var workspace = new Workspace(root);
            var repo = workspace.LoadRepo("app");
            var worktree = workspace.CreateWorktree(repo, "PROJ-3");
            var infra = Path.Combine(worktree.Path, "infra");
            Assert.True(File.Exists(Path.Combine(infra, "readme.txt"))); // CreateWorktree initialised the submodule
            Assert.Null(Worktrees.FindWorkAtRisk(worktree.Path));

            Git.Run(infra, "switch", "-c", "PROJ-3");
            File.WriteAllText(Path.Combine(infra, "ddl.sql"), "x");
            Git.Run(infra, "add", ".");
            Commit(infra, "ddl");

            var submodule = Assert.Single(Submodules.List(worktree.Path));
            Assert.Equal(("infra", "PROJ-3", true, 0, 1),
                (submodule.Name, submodule.Branch, submodule.PointerMoved, submodule.ChangedFiles, submodule.UnpushedCommits));

            // Even with the pointer committed and the parent clean, the unpushed submodule commit would be lost.
            Git.Run(worktree.Path, "add", "infra");
            Commit(worktree.Path, "bump infra");
            Assert.Equal(0, Worktrees.CountChangedFiles(worktree.Path));
            var refused = Assert.Throws<OverworkedException>(() => workspace.RemoveWorktree(repo, worktree, force: false));
            Assert.Contains("1 unpushed commit in infra", refused.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", null);
            Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", null);
            Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", null);
        }
    }

    [Fact]
    public void A_worktree_folder_still_in_use_is_removed_now_and_tidied_away_later()
    {
        if (!OperatingSystem.IsWindows()) return; // only Windows refuses to delete a folder a process is sitting in

        var root = NewRoot();
        CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);
        var worktree = workspace.CreateWorktree(workspace.LoadRepo("app"), "PROJ-4");
        // A shell sitting in the folder, like a VS Code terminal.
        using var sitter = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "powershell", "-NoProfile -Command Start-Sleep 30") { WorkingDirectory = worktree.Path, CreateNoWindow = true })!;
        try
        {
            Thread.Sleep(1500);
            var folderGone = workspace.RemoveWorktree(workspace.LoadRepo("app"), worktree, force: false);

            Assert.False(folderGone);                                          // Windows kept the folder...
            Assert.Empty(Directory.EnumerateFileSystemEntries(worktree.Path)); // ...but it's empty
            Assert.Single(workspace.LoadRepo("app").Worktrees);                // and git has let the worktree go
        }
        finally
        {
            sitter.Kill();
            sitter.WaitForExit();
        }

        workspace.LoadRepos(); // any later scan tidies it
        Assert.False(Directory.Exists(Path.Combine(root, ".worktrees")));
    }

    [Fact]
    public void Lists_and_switches_branches()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        Git.Run(app, "branch", "PROJ-7");
        Git.Run(app, "push", "origin", "PROJ-7");
        Git.Run(app, "branch", "-D", "PROJ-7");      // now only on origin
        Git.Run(app, "branch", "PROJ-8");            // only local
        var workspace = new Workspace(root);
        var busy = workspace.CreateWorktree(workspace.LoadRepo("app"), "PROJ-9");

        var branches = Branches.List(app).ToDictionary(b => b.Name);

        Assert.Equal(new[] { "PROJ-7", "PROJ-8", "PROJ-9", "main" }, branches.Keys.Order(StringComparer.Ordinal));
        Assert.True(branches["PROJ-7"].IsRemote);
        Assert.False(branches["PROJ-8"].IsRemote);
        Assert.Equal(Path.GetFullPath(app), branches["main"].CheckedOutAt);
        Assert.Equal(busy.Path, branches["PROJ-9"].CheckedOutAt);

        Branches.Switch(app, "PROJ-7", create: false);
        Assert.Equal("PROJ-7", Git.Run(app, "branch", "--show-current").Trim());
        Assert.Equal("origin/PROJ-7", Git.Run(app, "rev-parse", "--abbrev-ref", "PROJ-7@{upstream}").Trim());

        Branches.Switch(app, "PROJ-10", create: true);
        Assert.Equal("PROJ-10", Git.Run(app, "branch", "--show-current").Trim());
        Assert.Equal(Git.Run(app, "rev-parse", "origin/main"), Git.Run(app, "rev-parse", "HEAD"));

        Assert.Throws<OverworkedException>(() => Branches.Switch(app, "PROJ-9", create: false)); // in use by the worktree
    }

    [Fact]
    public void Tags_a_branch_with_its_pull_request_and_ticket_until_the_branch_is_deleted()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);
        var worktree = workspace.CreateWorktree(workspace.LoadRepo("app"), "feature/PROJ-11");
        const string pr = "https://github.com/example/app/pull/11";
        const string ticket = "https://example.atlassian.net/browse/PROJ-11";

        Assert.Throws<OverworkedException>(() => Tags.Set(app, "feature/PROJ-11", TagKind.PullRequest, "not a link"));
        Assert.Throws<OverworkedException>(() => Tags.Set(app, "feature/PROJ-11", TagKind.Ticket, "https://example.atlassian.net/jira/your-work"));
        Tags.Set(app, "feature/PROJ-11", TagKind.PullRequest, pr);
        Tags.Set(app, "feature/PROJ-11", TagKind.Ticket, ticket);

        var tagged = workspace.LoadRepo("app").Worktrees[1];
        Assert.Equal((pr, ticket, "PROJ-11"), (tagged.PullRequestUrl, tagged.TicketUrl, tagged.TicketKey));
        Assert.Equal((null, null), (workspace.LoadRepo("app").Main!.PullRequestUrl, workspace.LoadRepo("app").Main!.TicketUrl));

        workspace.RemoveWorktree(workspace.LoadRepo("app"), worktree, force: false);
        Git.Run(app, "branch", "-D", "feature/PROJ-11");
        workspace.CreateWorktree(workspace.LoadRepo("app"), "feature/PROJ-11");

        // A new branch of the same name starts untagged.
        var fresh = workspace.LoadRepo("app").Worktrees[1];
        Assert.Equal((null, null), (fresh.PullRequestUrl, fresh.TicketUrl));
    }

    [Fact]
    public void A_worktree_git_cannot_read_fails_its_repo_not_the_whole_scan()
    {
        var root = NewRoot();
        CloneWithOneCommit(root, "app");
        CloneWithOneCommit(root, "other");
        var workspace = new Workspace(root);
        var worktree = workspace.CreateWorktree(workspace.LoadRepo("app"), "PROJ-12");
        var gitFile = Path.Combine(worktree.Path, ".git");
        File.SetAttributes(gitFile, FileAttributes.Normal); // Git for Windows hides it, and hidden files can't be overwritten
        File.WriteAllText(gitFile, "gitdir: nowhere");      // so git status fails in it

        var repos = workspace.LoadRepos();

        Assert.NotNull(repos.Single(r => r.Name == "app").Error);
        Assert.Null(repos.Single(r => r.Name == "other").Error);
    }

    [Fact]
    public void A_rescan_that_finds_nothing_new_equals_the_last_one()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);

        var before = workspace.LoadRepo("app");
        Assert.Equal(before, workspace.LoadRepo("app"));

        File.WriteAllText(Path.Combine(app, "wip.txt"), "x");
        Assert.NotEqual(before, workspace.LoadRepo("app"));
    }

    string NewRoot() => Directory.CreateDirectory(Path.Combine(_dir, "repos")).FullName;

    string CloneWithOneCommit(string root, string name)
    {
        var origin = Path.Combine(_dir, $"{name}.git");
        Git.Run(_dir, "init", "--bare", "-b", "main", origin);
        Git.Run(root, "clone", origin, name);
        var app = Path.Combine(root, name);
        Git.Run(app, "symbolic-ref", "HEAD", "refs/heads/main");
        File.WriteAllText(Path.Combine(app, "readme.txt"), "hi");
        Git.Run(app, "add", ".");
        Commit(app, "init");
        Git.Run(app, "push", "origin", "main");
        return app;
    }

    static void Commit(string dir, string message) =>
        Git.Run(dir, "-c", "user.name=test", "-c", "user.email=test@test", "-c", "commit.gpgsign=false", "commit", "-m", message);

    public void Dispose()
    {
        // git marks object files read-only, which blocks Directory.Delete on Windows.
        foreach (var file in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_dir, recursive: true);
    }
}
