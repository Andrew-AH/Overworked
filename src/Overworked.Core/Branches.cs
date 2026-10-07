namespace Overworked.Core;

/// <summary>Listing, fetching and switching branches.</summary>
public static class Branches
{
    /// <summary>Where new branches start, whether made for a worktree or by switching.</summary>
    internal const string Base = "origin/main";

    const string LocalPrefix = "refs/heads/";
    const string OriginPrefix = "refs/remotes/origin/";

    /// <summary>Fetches origin, dropping branches deleted there.</summary>
    public static void Fetch(string repoPath) => Git.Run(repoPath, "fetch", "origin", "--prune");

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
        if (create) Git.Run(worktreePath, "switch", "--no-track", "-c", branch, Base);
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

    static (string Ref, string? CheckedOutAt, DateTimeOffset LastCommit) ParseLine(string line)
    {
        var fields = line.Split('\t');
        return (
            Ref: fields[0],
            CheckedOutAt: fields[1].Length > 0 ? Path.GetFullPath(fields[1]) : null,
            LastCommit: DateTimeOffset.FromUnixTimeSeconds(long.TryParse(fields[2], out var unix) ? unix : 0));
    }
}
