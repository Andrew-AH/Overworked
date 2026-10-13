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
            // A change inside the submodule isn't the parent's, so a scan doesn't count it; removing would still lose it.
            Assert.Equal(0, Worktrees.CountChangedFiles(worktree.Path));
            Assert.Equal("1 uncommitted change in infra", Worktrees.FindWorkAtRisk(worktree.Path)!.Describe());
            Git.Run(infra, "add", ".");
            Commit(infra, "ddl");
            Assert.Equal(1, Worktrees.CountChangedFiles(worktree.Path)); // the submodule's commit moved: that, the parent sees

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
    public void Cleans_up_after_a_branch_deleted_on_origin_deleting_it_only_if_origin_had_all_of_it()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);
        var merged = PushedWorktree(workspace, "feature-a");
        var ahead = PushedWorktree(workspace, "feature-b");
        CommitFile(ahead.Path, "later.txt", "a commit origin never gets");
        DeleteOnOrigin("app", "feature-a", "feature-b"); // GitHub squash-merges both PRs and deletes the branches

        Assert.DoesNotContain(workspace.LoadRepo("app").Linked, w => w.BranchGone); // nothing to tell until a fetch
        Branches.Fetch(app);
        var repo = workspace.LoadRepo("app");
        Assert.All(repo.Linked, w => Assert.True(w.BranchGone));

        Assert.Equal(new CleanUpResult(BranchDeleted: true), workspace.CleanUp(repo, repo.Linked.Single(w => w.Branch == "feature-a")));
        Assert.Equal(new CleanUpResult(BranchDeleted: false), workspace.CleanUp(repo, repo.Linked.Single(w => w.Branch == "feature-b")));

        Assert.Empty(workspace.LoadRepo("app").Linked);
        Assert.False(Git.Succeeds(app, "show-ref", "--verify", "--quiet", "refs/heads/feature-a")); // all on origin: gone
        Assert.True(Git.Succeeds(app, "show-ref", "--verify", "--quiet", "refs/heads/feature-b"));  // had more: kept
    }

    [Fact]
    public void Cleaning_up_keeps_uncommitted_work_and_branches_it_cant_vouch_for()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);
        var dirty = PushedWorktree(workspace, "feature-c");
        PushedWorktree(workspace, "feature-d");
        PushedWorktree(workspace, "feature-e");
        File.WriteAllText(Path.Combine(dirty.Path, "wip.txt"), "x");
        DeleteOnOrigin("app", "feature-c", "feature-d");
        Git.Run(app, "fetch", "--prune", "origin"); // pruned by something else, so there's no note of what origin had

        var repo = workspace.LoadRepo("app");
        Worktree Linked(string branch) => repo.Linked.Single(w => w.Branch == branch);

        Assert.Throws<OverworkedException>(() => workspace.CleanUp(repo, Linked("feature-c"))); // uncommitted work
        Assert.True(Directory.Exists(dirty.Path));
        Assert.Equal(new CleanUpResult(BranchDeleted: false), workspace.CleanUp(repo, Linked("feature-d")));
        Assert.True(Git.Succeeds(app, "show-ref", "--verify", "--quiet", "refs/heads/feature-d")); // can't tell: kept
        Assert.Throws<OverworkedException>(() => workspace.CleanUp(repo, Linked("feature-e")));  // still on origin
    }

    [Fact]
    public void Cleaning_up_the_main_folder_switches_it_back_to_main()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);
        Branches.Switch(app, "feature-f", create: true);
        CommitFile(app, "f.txt", "work on feature-f");
        Git.Run(app, "push", "-q", "-u", "origin", "feature-f");
        DeleteOnOrigin("app", "feature-f");
        Branches.Fetch(app);
        File.WriteAllText(Path.Combine(app, "wip.txt"), "x");

        var main = workspace.LoadRepo("app").Main!;
        Assert.True(main.BranchGone);
        Assert.Throws<OverworkedException>(() => workspace.CleanUp(workspace.LoadRepo("app"), main)); // uncommitted work
        File.Delete(Path.Combine(app, "wip.txt"));

        Assert.Equal(new CleanUpResult(BranchDeleted: true, SwitchedTo: "main"), workspace.CleanUp(workspace.LoadRepo("app"), main));
        Assert.Equal("main", Git.Run(app, "branch", "--show-current").Trim());
        Assert.False(Git.Succeeds(app, "show-ref", "--verify", "--quiet", "refs/heads/feature-f"));
    }

    [Fact]
    public void Counts_commits_origin_does_not_have_yet()
    {
        var root = NewRoot();
        CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);
        var worktree = workspace.CreateWorktree(workspace.LoadRepo("app"), "PROJ-15");
        int Unpushed() => workspace.LoadRepo("app").Linked.Single().UnpushedCommits;

        Assert.Equal(0, Unpushed()); // a new branch is just origin/main so far
        CommitFile(worktree.Path, "a.txt", "one");
        CommitFile(worktree.Path, "b.txt", "two");
        Assert.Equal(2, Unpushed()); // never pushed: no upstream to compare with
        Git.Run(worktree.Path, "push", "-q", "-u", "origin", "PROJ-15");
        Assert.Equal(0, Unpushed());
        CommitFile(worktree.Path, "c.txt", "three");
        Assert.Equal(1, Unpushed()); // ahead of its upstream
        Assert.Equal(0, workspace.LoadRepo("app").Main!.UnpushedCommits);
    }

    [Fact]
    public void Copies_ignored_files_named_in_worktreeinclude_into_a_new_worktree()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        File.WriteAllText(Path.Combine(app, ".gitignore"), "*.local.json\n.vscode/\nbin/\n");
        Git.Run(app, "add", ".gitignore");
        Commit(app, "ignore local files");
        Git.Run(app, "push", "-q", "origin", "main");
        Write("Svc/appsettings.local.json", "secrets");
        Write(".vscode/settings.json", "{}");
        Write("Svc/bin/app.dll", "build output");
        Write("notes.json", "untracked, but not ignored");
        // Kept with the repo's worktrees, not in the repo.
        Directory.CreateDirectory(Path.Combine(root, ".worktrees", "app"));
        File.WriteAllText(Path.Combine(root, ".worktrees", "app", ".worktreeinclude"), "**/appsettings.local.json\n.vscode/\n*.json\n");
        var workspace = new Workspace(root);

        var worktree = workspace.CreateWorktree(workspace.LoadRepo("app"), "PROJ-16");

        Assert.Equal("secrets", File.ReadAllText(Path.Combine(worktree.Path, "Svc", "appsettings.local.json")));
        Assert.True(File.Exists(Path.Combine(worktree.Path, ".vscode", "settings.json")));
        Assert.False(File.Exists(Path.Combine(worktree.Path, "Svc", "bin", "app.dll"))); // ignored, but not asked for
        Assert.False(File.Exists(Path.Combine(worktree.Path, "notes.json")));            // asked for, but not ignored
        Assert.Equal(0, workspace.LoadRepo("app").Linked.Single().ChangedFiles);         // copies stay ignored there

        void Write(string file, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(app, file))!);
            File.WriteAllText(Path.Combine(app, file), text);
        }
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

    [Fact]
    public void A_repo_whose_default_branch_is_master_branches_from_origin_master()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app", defaultBranch: "master");
        Git.Run(app, "remote", "set-head", "origin", "-d"); // no record of origin's HEAD (git init, then remote add): guessed
        var workspace = new Workspace(root);

        workspace.CreateWorktree(workspace.LoadRepo("app"), "PROJ-13");
        Branches.Switch(app, "PROJ-14", create: true);

        Assert.Equal(Git.Run(app, "rev-parse", "origin/master"), Git.Run(app, "rev-parse", "PROJ-13"));
        Assert.Equal(Git.Run(app, "rev-parse", "origin/master"), Git.Run(app, "rev-parse", "PROJ-14"));
    }

    [Fact]
    public void Branches_from_origins_default_branch_whatever_its_name()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app", defaultBranch: "develop");
        var workspace = new Workspace(root);

        var repo = workspace.LoadRepo("app");
        Assert.Equal("develop", repo.DefaultBranch);
        Assert.False(repo.MayHaveGoneBranches); // its own folder on develop is at rest, not a branch out
        workspace.CreateWorktree(repo, "PROJ-17");
        Branches.Switch(app, "PROJ-18", create: true);
        Assert.Equal(Git.Run(app, "rev-parse", "origin/develop"), Git.Run(app, "rev-parse", "PROJ-17"));
        Assert.Equal(Git.Run(app, "rev-parse", "origin/develop"), Git.Run(app, "rev-parse", "PROJ-18"));

        // Clean up returns the repo's own folder to develop, not main.
        CommitFile(app, "18.txt", "work on PROJ-18");
        Git.Run(app, "push", "-q", "-u", "origin", "PROJ-18");
        DeleteOnOrigin("app", "PROJ-18");
        Branches.Fetch(app);
        Assert.Equal(new CleanUpResult(BranchDeleted: true, SwitchedTo: "develop"),
            workspace.CleanUp(workspace.LoadRepo("app"), workspace.LoadRepo("app").Main!));
        Assert.Equal("develop", Git.Run(app, "branch", "--show-current").Trim());
    }

    [Fact]
    public void A_branch_pushed_without_tracking_is_tracked_after_a_fetch_so_its_merge_still_shows()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        var workspace = new Workspace(root);
        var g = workspace.CreateWorktree(workspace.LoadRepo("app"), "feature-g");
        var h = workspace.CreateWorktree(workspace.LoadRepo("app"), "feature-h");
        CommitFile(g.Path, "g.txt", "work on feature-g");
        CommitFile(h.Path, "h.txt", "work on feature-h");
        Git.Run(g.Path, "push", "-q", "origin", "feature-g"); // a plain push, no -u: git doesn't track origin's copy
        Git.Run(h.Path, "push", "-q", "origin", "feature-h");
        Assert.False(Git.Succeeds(app, "rev-parse", "--abbrev-ref", "feature-g@{upstream}"));
        DeleteOnOrigin("app", "feature-h"); // merged before any fetch saw it on origin

        Branches.Fetch(app);

        Assert.Equal("origin/feature-g", Git.Run(app, "rev-parse", "--abbrev-ref", "feature-g@{upstream}").Trim());
        var repo = workspace.LoadRepo("app");
        Worktree Linked(string branch) => repo.Linked.Single(w => w.Branch == branch);
        Assert.Equal((false, 0), (Linked("feature-g").BranchGone, Linked("feature-g").UnpushedCommits));
        Assert.True(Linked("feature-h").BranchGone);
        Assert.Equal(new CleanUpResult(BranchDeleted: true), workspace.CleanUp(repo, Linked("feature-h"))); // origin had all of it

        DeleteOnOrigin("app", "feature-g");
        Branches.Fetch(app);
        repo = workspace.LoadRepo("app");
        Assert.True(Linked("feature-g").BranchGone);
        Assert.Equal(new CleanUpResult(BranchDeleted: true), workspace.CleanUp(repo, Linked("feature-g")));
    }

    [Fact]
    public void A_new_branch_named_after_a_ticket_is_tagged_with_it_once_its_project_is_known()
    {
        var root = NewRoot();
        var app = CloneWithOneCommit(root, "app");
        CloneWithOneCommit(root, "other");
        var workspace = new Workspace(root);
        Worktree Open(string repo, string branch) => workspace.CreateWorktree(workspace.LoadRepo(repo), branch);

        Assert.Null(Open("app", "proj-30-first").TicketUrl); // nothing tagged anywhere yet: nothing to go by
        Tags.Set(app, "proj-30-first", TagKind.Ticket, "https://example.atlassian.net/browse/PROJ-30");

        Assert.Equal("https://example.atlassian.net/browse/PROJ-31", Open("other", "proj-31-next").TicketUrl); // any repo
        Assert.Equal("PROJ-31", workspace.LoadRepo("other").Linked.Single().TicketKey);
        Assert.Null(Open("app", "fix-32-typo").TicketUrl); // FIX isn't a project anyone has tagged

        // An existing branch keeps the ticket it was tagged with.
        Git.Run(app, "branch", "proj-33-old");
        Tags.Set(app, "proj-33-old", TagKind.Ticket, "https://example.atlassian.net/browse/PROJ-30");
        Assert.Equal("https://example.atlassian.net/browse/PROJ-30", Open("app", "proj-33-old").TicketUrl);
    }

    string NewRoot() => Directory.CreateDirectory(Path.Combine(_dir, "repos")).FullName;

    string CloneWithOneCommit(string root, string name, string defaultBranch = "main")
    {
        var origin = Path.Combine(_dir, $"{name}.git");
        Git.Run(_dir, "init", "--bare", "-b", defaultBranch, origin);
        Git.Run(root, "clone", origin, name);
        var app = Path.Combine(root, name);
        Git.Run(app, "symbolic-ref", "HEAD", $"refs/heads/{defaultBranch}");
        File.WriteAllText(Path.Combine(app, "readme.txt"), "hi");
        Git.Run(app, "add", ".");
        Commit(app, "init");
        Git.Run(app, "push", "origin", defaultBranch);
        Git.Run(app, "remote", "set-head", "origin", "-a"); // as a clone of a non-empty repo records it
        return app;
    }

    /// <summary>A worktree with a commit, pushed so its branch tracks origin's, as opening a PR does.</summary>
    static Worktree PushedWorktree(Workspace workspace, string branch)
    {
        var worktree = workspace.CreateWorktree(workspace.LoadRepo("app"), branch);
        CommitFile(worktree.Path, $"{branch}.txt", $"work on {branch}");
        Git.Run(worktree.Path, "push", "-q", "-u", "origin", branch);
        return worktree;
    }

    static void CommitFile(string dir, string file, string message)
    {
        File.WriteAllText(Path.Combine(dir, file), message);
        Git.Run(dir, "add", file);
        Commit(dir, message);
    }

    /// <summary>Deletes branches on the origin itself, as GitHub does after a merge; this clone doesn't know yet.</summary>
    void DeleteOnOrigin(string repo, params string[] branches)
    {
        foreach (var branch in branches) Git.Run(Path.Combine(_dir, $"{repo}.git"), "branch", "-D", branch);
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
