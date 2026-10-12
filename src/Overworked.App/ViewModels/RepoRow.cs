using Overworked.Core;

namespace Overworked.App.ViewModels;

/// <summary>A repo's row: its name and main checkout, with its linked worktrees beneath.</summary>
public sealed class RepoRow : RowViewModel
{
    bool _isExpanded;

    public RepoRow(Repo repo, bool isExpanded, RowColours colours, IReadOnlyDictionary<string, string> busy) : base(repo, colours, busy)
    {
        _isExpanded = isExpanded;
        var linked = repo.Linked.ToList();
        Worktrees = linked.Select((w, i) => new WorktreeRow(repo, w, colours, busy, isLast: i == linked.Count - 1)).ToList();
    }

    public override Worktree? Worktree => Repo.Main;
    public override string Path => Repo.Path;
    public override string Title => Name;
    public string Name => Repo.Name;

    public override bool IsBroken => Repo.Error is not null || base.IsBroken;
    public override string Detail => Repo.Error?.Split('\n')[0] ?? (IsGone ? $"{Branch} · deleted on origin" : base.Detail);

    public IReadOnlyList<WorktreeRow> Worktrees { get; }
    public bool HasWorktrees => Worktrees.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (Set(ref _isExpanded, value)) OnPropertyChanged(nameof(ShowsRail));
        }
    }

    /// <summary>The lane's line runs down to the worktrees while they're shown.</summary>
    public bool ShowsRail => HasWorktrees && IsExpanded;
}
