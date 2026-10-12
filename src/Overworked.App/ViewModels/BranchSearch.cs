using Overworked.Core;

namespace Overworked.App.ViewModels;

/// <summary>A branch the user picked: an existing one, or a new name to create from origin's default branch.</summary>
public sealed record BranchChoice(string Name, bool Create);

/// <summary>A row in a branch picker.</summary>
/// <param name="Tag">Short right-hand label: an age, "origin", "worktree", "main folder" or "from main".</param>
/// <param name="CanPick">False for a branch already checked out somewhere; git allows one place per branch.</param>
/// <param name="IsNew">The offer to create a branch with the typed name.</param>
public sealed record BranchOption(string Name, string Tag, string? Tip, bool CanPick, bool IsNew)
{
    public string Label => IsNew ? $"+ Create {Name}" : Name;

    public BranchChoice Choice => new(Name, Create: IsNew);
}

/// <summary>Narrowing a list of branches as you type, VS Code style.</summary>
public static class BranchSearch
{
    /// <summary>
    /// The options for <paramref name="query"/>: branches you can pick, then ones checked out somewhere; within each,
    /// prefix matches, then substring, then in-order letters, otherwise keeping <paramref name="branches"/>' order.
    /// Then, unless a branch has exactly that name, an offer to create it. Last, so Enter on a half-typed name picks an
    /// existing branch rather than creating a stray one.
    /// </summary>
    /// <param name="exclude">A branch to leave out: the one already checked out, when switching.</param>
    /// <param name="mainFolder">The repo's own folder, to tell its branch apart from a worktree's.</param>
    /// <param name="defaultBranch">Origin's default branch, which a new branch is created from.</param>
    public static List<BranchOption> Filter(IReadOnlyList<BranchRef> branches, string query, string? exclude,
        string mainFolder, DateTimeOffset now, string defaultBranch = "main")
    {
        var options = branches
            .Where(b => b.Name != exclude)
            .Select(b => (Branch: b, Score: Score(b.Name, query)))
            .Where(x => x.Score is not null)
            .Select(x => (Option: Describe(x.Branch, mainFolder, now), x.Score))
            .OrderBy(x => x.Option.CanPick ? 0 : 1).ThenBy(x => x.Score)
            .Select(x => x.Option)
            .ToList();
        if (query.Length > 0 && !branches.Any(b => b.Name.Equals(query, StringComparison.OrdinalIgnoreCase)))
            options.Add(new BranchOption(query, $"from {defaultBranch}", $"New branch from origin/{defaultBranch}", CanPick: true, IsNew: true));
        return options;
    }

    /// <summary>How well a branch name matches: 0 for a prefix (or an empty query), 1 a substring, 2 in-order letters, null no match.</summary>
    public static int? Score(string name, string query)
    {
        if (query.Length == 0) return 0;
        var at = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (at >= 0) return at == 0 ? 0 : 1;
        var matched = 0;
        foreach (var c in name)
            if (matched < query.Length && char.ToLowerInvariant(c) == char.ToLowerInvariant(query[matched])) matched++;
        return matched == query.Length ? 2 : null;
    }

    /// <summary>Compact age: "5m", "3h", "12d", or the date once it's a month old.</summary>
    public static string Age(DateTimeOffset when, DateTimeOffset now)
    {
        var age = now - when;
        return age.TotalHours < 1 ? $"{Math.Max(1, (int)age.TotalMinutes)}m"
            : age.TotalDays < 1 ? $"{(int)age.TotalHours}h"
            : age.TotalDays < 30 ? $"{(int)age.TotalDays}d"
            : when.ToString("d MMM");
    }

    static BranchOption Describe(BranchRef branch, string mainFolder, DateTimeOffset now) =>
        branch.CheckedOutAt is { } at && string.Equals(at, Path.GetFullPath(mainFolder), StringComparison.OrdinalIgnoreCase)
            ? new BranchOption(branch.Name, "main folder", "Checked out in the main folder", CanPick: false, IsNew: false)
        : branch.CheckedOutAt is { } elsewhere
            ? new BranchOption(branch.Name, "worktree", $"Already open in {Path.GetFileName(elsewhere)}", CanPick: false, IsNew: false)
        : branch.IsRemote
            ? new BranchOption(branch.Name, "origin", $"Only on origin · last commit {Age(branch.LastCommit, now)} ago", CanPick: true, IsNew: false)
        : new BranchOption(branch.Name, Age(branch.LastCommit, now), Tip: null, CanPick: true, IsNew: false);
}
