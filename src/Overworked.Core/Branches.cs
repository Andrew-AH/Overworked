using System.Text.RegularExpressions;

namespace Overworked.Core;

/// <summary>Listing, fetching and switching branches.</summary>
public static class Branches
{
    const string LocalPrefix = "refs/heads/";
    const string OriginPrefix = "refs/remotes/origin/";

    /// <summary>Where new branches start, whether made for a worktree or by switching: origin's default branch.</summary>
    internal static string Base(string repoPath) => "origin/" + DefaultBranch(repoPath);

    /// <summary>
    /// Origin's default branch, whatever its name (main, develop, trunk…): where origin's HEAD points, as a clone records
    /// it. A repo with no such record (git init, then remote add) is taken to use main, or master if it has no main;
    /// `git remote set-head origin -a` records the real one.
    /// </summary>
    public static string DefaultBranch(string repoPath)
    {
        try { return Git.Run(repoPath, "symbolic-ref", "refs/remotes/origin/HEAD").Trim()[OriginPrefix.Length..]; }
        catch (OverworkedException) { }
        return Git.Succeeds(repoPath, "show-ref", "--verify", "--quiet", OriginPrefix + "main") ? "main" : "master";
    }

    // git config: the last commit origin had of a branch, noted as its copy there was deleted.
    const string LastPushedSetting = "overworked-last-pushed";

    /// <summary>
    /// Fetches origin, dropping branches deleted there. A local branch whose copy on origin was just deleted (as when
    /// its pull request is merged) first gets a note of the last commit origin had, so <see cref="WasFullyPushed"/>
    /// can tell afterwards whether everything on it got there.
    /// </summary>
    public static void Fetch(string repoPath)
    {
        var tracking = Track(repoPath);
        var before = OriginTips(repoPath);
        Git.Run(repoPath, "fetch", "origin", "--prune");
        var after = OriginTips(repoPath);
        foreach (var (branch, state) in tracking)
        {
            var upstream = state.Upstream;
            // Pushed with a plain `git push origin <branch>`, so git doesn't track origin's copy and would never report it
            // gone. Track it now, as `git push -u` would have.
            if (upstream is null && (before.ContainsKey(OriginPrefix + branch) || after.ContainsKey(OriginPrefix + branch)))
            {
                Git.Run(repoPath, "config", "--local", $"branch.{branch}.remote", "origin");
                Git.Run(repoPath, "config", "--local", $"branch.{branch}.merge", LocalPrefix + branch);
                upstream = OriginPrefix + branch;
            }
            if (upstream is not null && before.TryGetValue(upstream, out var lastTip) && !after.ContainsKey(upstream))
                Git.Run(repoPath, "config", "--local", $"branch.{branch}.{LastPushedSetting}", lastTip);
        }
    }

    /// <summary><see cref="Fetch"/> for several repos at once. What failed, one line per repo: "app: &lt;git's error&gt;".</summary>
    public static IReadOnlyList<string> FetchAll(IEnumerable<Repo> repos) =>
        repos.AsParallel()
            .Select(repo =>
            {
                try { Fetch(repo.Path); return null; }
                catch (OverworkedException e) { return $"{repo.Name}: {e.Message.Split('\n')[0]}"; }
            })
            .OfType<string>()
            .ToList();

    /// <summary>Each local branch's standing against its upstream, by name.</summary>
    internal static IReadOnlyDictionary<string, Tracking> Track(string repoPath) =>
        ParseTracking(Git.Run(repoPath, "for-each-ref", "--format=%(refname:short)%09%(upstream)%09%(upstream:track)", "refs/heads"));

    /// <summary>
    /// Commits on <paramref name="branch"/> that origin doesn't have: those ahead of its upstream, or for a branch never
    /// pushed with -u, those on no remote branch. None for a branch deleted on origin; <see cref="WasFullyPushed"/> judges those.
    /// </summary>
    internal static int CountUnpushed(string repoPath, string branch, Tracking tracking) =>
        tracking.Gone ? 0
        : tracking.Upstream is not null ? tracking.Ahead
        : int.Parse(Git.Run(repoPath, "rev-list", "--count", LocalPrefix + branch, "--not", "--remotes").Trim());

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

    /// <summary>
    /// `for-each-ref` lines of a branch, its upstream and how it stands against it, tab-separated: "[gone]" once a fetch
    /// has pruned the upstream, "[ahead 2]", "[ahead 2, behind 3]", or nothing when level or there's no upstream.
    /// </summary>
    internal static Dictionary<string, Tracking> ParseTracking(string forEachRef) =>
        Git.Lines(forEachRef)
            .Select(line => line.Split('\t'))
            .ToDictionary(fields => fields[0], fields =>
            {
                var track = fields.Length > 2 ? fields[2] : "";
                return new Tracking(
                    Upstream: fields.Length > 1 && fields[1].Length > 0 ? fields[1] : null,
                    Gone: track == "[gone]",
                    Ahead: Regex.Match(track, @"ahead (\d+)") is { Success: true } ahead ? int.Parse(ahead.Groups[1].Value) : 0);
            });

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

/// <summary>How a local branch stands against its upstream (<see cref="Branches.Track"/>).</summary>
/// <param name="Upstream">The ref it tracks, e.g. refs/remotes/origin/PROJ-1; null for a branch never pushed with -u.</param>
/// <param name="Gone">Its upstream was deleted on origin, as when its pull request was merged.</param>
/// <param name="Ahead">Commits it has that its upstream doesn't.</param>
internal readonly record struct Tracking(string? Upstream, bool Gone, int Ahead);
