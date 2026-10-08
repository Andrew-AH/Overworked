namespace Overworked.Core;

/// <summary>Listing, fetching and switching branches.</summary>
public static class Branches
{
    const string LocalPrefix = "refs/heads/";
    const string OriginPrefix = "refs/remotes/origin/";

    /// <summary>Where new branches start, whether made for a worktree or by switching: origin/main, or origin/master in repos without a main.</summary>
    internal static string Base(string repoPath) =>
        Git.Succeeds(repoPath, "show-ref", "--verify", "--quiet", OriginPrefix + "main") ? "origin/main" : "origin/master";

    // git config: the last commit origin had of a branch, noted as its copy there was deleted.
    const string LastPushedSetting = "overworked-last-pushed";

    /// <summary>
    /// Fetches origin, dropping branches deleted there. A local branch whose copy on origin was just deleted (as when
    /// its pull request is merged) first gets a note of the last commit origin had, so <see cref="WasFullyPushed"/>
    /// can tell afterwards whether everything on it got there.
    /// </summary>
    public static void Fetch(string repoPath)
    {
        var upstreams = Upstreams(repoPath);
        var before = OriginTips(repoPath);
        Git.Run(repoPath, "fetch", "origin", "--prune");
        var after = OriginTips(repoPath);
        foreach (var (branch, upstream) in upstreams)
            if (before.TryGetValue(upstream, out var lastTip) && !after.ContainsKey(upstream))
                Git.Run(repoPath, "config", "--local", $"branch.{branch}.{LastPushedSetting}", lastTip);
    }

    /// <summary>Local branches whose copy on origin was deleted. git reports them "[gone]" once a fetch has pruned it.</summary>
    internal static IReadOnlySet<string> Gone(string repoPath) =>
        ParseGone(Git.Run(repoPath, "for-each-ref", "--format=%(refname:short)%09%(upstream:track)", "refs/heads"));

    /// <summary>Whether origin had every commit on <paramref name="branch"/> when its copy there was deleted.</summary>
    /// <returns>False when there's anything newer, or no note to tell by (it was deleted before Overworked fetched).</returns>
    internal static bool WasFullyPushed(string repoPath, string branch)
    {
        string lastTip;
        try { lastTip = Git.Run(repoPath, "config", "--local", "--get", $"branch.{branch}.{LastPushedSetting}").Trim(); }
        catch (OverworkedException) { return false; }
        return Git.Succeeds(repoPath, "merge-base", "--is-ancestor", LocalPrefix + branch, lastTip);
    }

    /// <summary>Deletes a local branch, its config (tags, notes) with it.</summary>
    internal static void Delete(string repoPath, string branch) => Git.Run(repoPath, "branch", "-D", branch);

    /// <summary>Local branches, then origin branches with no local copy, each newest first.</summary>
    public static IReadOnlyList<BranchRef> List(string repoPath) =>
        Parse(Git.Run(repoPath, "for-each-ref", "--sort=-committerdate",
            "--format=%(refname)%09%(worktreepath)%09%(committerdate:unix)", "refs/heads", "refs/remotes/origin"));

    /// <summary>
    /// Switches the checkout at <paramref name="worktreePath"/> to <paramref name="branch"/>. A branch only on origin
    /// becomes a local one tracking it; <paramref name="create"/> makes a brand-new branch from origin/main, like a new
    /// worktree does. Submodules are left where they are, so any work on a submodule branch stays put.
    /// </summary>
    public static void Switch(string worktreePath, string branch, bool create)
    {
        branch = RequireName(branch);
        if (create) Git.Run(worktreePath, "switch", "--no-track", "-c", branch, Base(worktreePath));
        else Git.Run(worktreePath, "switch", branch);
    }

    /// <summary>Whether the repo has this branch, locally or on origin.</summary>
    internal static bool Exists(string repoPath, string branch) =>
        Git.Succeeds(repoPath, "show-ref", "--verify", "--quiet", LocalPrefix + branch)
        || Git.Succeeds(repoPath, "show-ref", "--verify", "--quiet", OriginPrefix + branch);

    /// <summary>A branch name as the user typed it, trimmed.</summary>
    /// <exception cref="OverworkedException">It's blank.</exception>
    internal static string RequireName(string branch) =>
        branch.Trim() is { Length: > 0 } name ? name : throw new OverworkedException("Branch name is required.");

    /// <summary>The output of <see cref="List"/>'s for-each-ref: per line, the ref, its worktree and its commit time.</summary>
    internal static IReadOnlyList<BranchRef> Parse(string forEachRef)
    {
        var refs = Git.Lines(forEachRef).Select(ParseLine).ToList();

        var local = refs
            .Where(r => r.Ref.StartsWith(LocalPrefix))
            .Select(r => new BranchRef(r.Ref[LocalPrefix.Length..], IsRemote: false, r.CheckedOutAt, r.LastCommit))
            .ToList();
        var localNames = local.Select(b => b.Name).ToHashSet();
        var originOnly = refs
            .Where(r => r.Ref.StartsWith(OriginPrefix) && r.Ref != OriginPrefix + "HEAD")
            .Select(r => new BranchRef(r.Ref[OriginPrefix.Length..], IsRemote: true, CheckedOutAt: null, r.LastCommit))
            .Where(b => !localNames.Contains(b.Name));

        return [.. local, .. originOnly];
    }

    /// <summary>`for-each-ref` lines of a branch and its upstream's state, tab-separated; "[gone]" when deleted on origin.</summary>
    internal static IReadOnlySet<string> ParseGone(string forEachRef) =>
        Git.Lines(forEachRef)
            .Select(line => line.Split('\t'))
            .Where(fields => fields.Length > 1 && fields[1] == "[gone]")
            .Select(fields => fields[0])
            .ToHashSet();

    /// <summary>Each local branch's upstream ref, for those that have one.</summary>
    static Dictionary<string, string> Upstreams(string repoPath) =>
        Git.Lines(Git.Run(repoPath, "for-each-ref", "--format=%(refname:short)%09%(upstream)", "refs/heads"))
            .Select(line => line.Split('\t'))
            .Where(fields => fields.Length > 1 && fields[1].Length > 0)
            .ToDictionary(fields => fields[0], fields => fields[1]);

    /// <summary>The commit each of origin's branches is on, by ref.</summary>
    static Dictionary<string, string> OriginTips(string repoPath) =>
        Git.Lines(Git.Run(repoPath, "for-each-ref", "--format=%(refname)%09%(objectname)", "refs/remotes/origin"))
            .Select(line => line.Split('\t'))
            .ToDictionary(fields => fields[0], fields => fields[1]);

    static (string Ref, string? CheckedOutAt, DateTimeOffset LastCommit) ParseLine(string line)
    {
        var fields = line.Split('\t');
        return (
            Ref: fields[0],
            CheckedOutAt: fields[1].Length > 0 ? Path.GetFullPath(fields[1]) : null,
            LastCommit: DateTimeOffset.FromUnixTimeSeconds(long.TryParse(fields[2], out var unix) ? unix : 0));
    }
}
