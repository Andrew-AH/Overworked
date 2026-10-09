using Overworked.Core;

namespace Overworked.Core.Tests;

/// <summary>git's porcelain output, parsed without running git.</summary>
public sealed class ParsingTests
{
    [Fact]
    public void Parses_detached_and_missing_worktrees()
    {
        var worktrees = Worktrees.Parse("""
            worktree /r/app
            HEAD aaa
            branch refs/heads/main

            worktree /r/gone
            HEAD bbb
            detached
            prunable gitdir file points to non-existent location

            """);

        Assert.Equal(("main", true, false), (worktrees[0].Branch, worktrees[0].IsMain, worktrees[0].IsMissing));
        Assert.Equal((null, false, true), (worktrees[1].Branch, worktrees[1].IsMain, worktrees[1].IsMissing));
    }

    [Fact]
    public void Lists_local_branches_then_ones_only_on_origin()
    {
        var checkout = Path.GetFullPath("/r/app");
        var branches = Branches.Parse(
            $"refs/heads/main\t{checkout}\t1700000300\n" +
            "refs/remotes/origin/PROJ-7\t\t1700000200\n" +
            "refs/heads/PROJ-8\t\t1700000100\n" +
            "refs/remotes/origin/HEAD\t\t1700000300\n" +
            "refs/remotes/origin/main\t\t1700000300\n");

        Assert.Equal(new[] { "main", "PROJ-8", "PROJ-7" }, branches.Select(b => b.Name)); // origin/main has a local copy
        Assert.Equal(new[] { false, false, true }, branches.Select(b => b.IsRemote));
        Assert.Equal(checkout, branches[0].CheckedOutAt);
        Assert.Null(branches[1].CheckedOutAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000100), branches[1].LastCommit);
    }

    [Fact]
    public void Reads_how_each_branch_stands_against_origin()
    {
        var tracking = Branches.ParseTracking(
            "main\trefs/remotes/origin/main\t\n" +
            "feature-a\trefs/remotes/origin/feature-a\t[gone]\n" +
            "feature-b\trefs/remotes/origin/feature-b\t[ahead 1]\n" +
            "feature/c\trefs/remotes/origin/feature/c\t[ahead 12, behind 3]\n" +
            "feature-d\trefs/remotes/origin/feature-d\t[behind 2]\n" +
            "local-only\t\t\n");

        Assert.Equal(new Tracking("refs/remotes/origin/main", Gone: false, Ahead: 0), tracking["main"]);
        Assert.Equal(new Tracking("refs/remotes/origin/feature-a", Gone: true, Ahead: 0), tracking["feature-a"]);
        Assert.Equal(1, tracking["feature-b"].Ahead);
        Assert.Equal(12, tracking["feature/c"].Ahead);
        Assert.Equal(0, tracking["feature-d"].Ahead);
        Assert.Equal(new Tracking(null, Gone: false, Ahead: 0), tracking["local-only"]);
    }

    [Fact]
    public void Finds_submodules_in_the_index_but_not_conflicted_ones()
    {
        var links = Submodules.ParseGitlinks(
            "100644 1111111111111111111111111111111111111111 0\treadme.txt\n" +
            "160000 2222222222222222222222222222222222222222 0\tlibs/my infra\n" +
            "160000 3333333333333333333333333333333333333333 2\tconflicted\n");

        var link = Assert.Single(links);
        Assert.Equal(("libs/my infra", "2222222222222222222222222222222222222222"), (link.Path, link.Commit));
    }

    [Fact]
    public void Reads_branch_commit_and_changes_from_status()
    {
        var status = Submodules.ParseStatus("""
            # branch.oid abc123
            # branch.head PROJ-3
            1 .M N... 100644 100644 100644 aaa bbb ddl.sql
            ? new.txt

            """);
        var detached = Submodules.ParseStatus("# branch.oid abc123\n# branch.head (detached)\n");

        Assert.Equal(("PROJ-3", "abc123", 2), (status.Branch, status.Commit, status.ChangedFiles));
        Assert.Equal((null, 0), (detached.Branch, detached.ChangedFiles));
    }

    [Fact]
    public void Reads_pull_request_and_ticket_tags_from_git_config()
    {
        var tags = Tags.Parse(
            "branch.feature/PROJ-1.overworked-pr https://github.com/example/app/pull/1\n" +
            "branch.feature/PROJ-1.overworked-ticket https://example.atlassian.net/browse/PROJ-1\n" +
            "branch.v1.2.overworked-pr https://github.com/example/app/pull/2\n" +
            "branch.proj-3.overworked-ticket https://example.atlassian.net/browse/PROJ-3\n");

        Assert.Equal(new BranchTags("https://github.com/example/app/pull/1", "https://example.atlassian.net/browse/PROJ-1"),
            tags["feature/PROJ-1"]);
        Assert.Equal(new BranchTags("https://github.com/example/app/pull/2", null), tags["v1.2"]);
        Assert.Equal(new BranchTags(null, "https://example.atlassian.net/browse/PROJ-3"), tags["proj-3"]);
    }
}
