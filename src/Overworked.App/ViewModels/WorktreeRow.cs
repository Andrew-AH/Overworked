using Overworked.Core;

namespace Overworked.App.ViewModels;

/// <summary>A linked worktree's row, drawn as a branch off its repo's lane.</summary>
public sealed class WorktreeRow(Repo repo, Worktree worktree, RowColours colours, bool isLast) : RowViewModel(repo, colours)
{
    public override Worktree Worktree { get; } = worktree;
    public override string Path => Worktree.Path;

    /// <summary>Its branch; the folder name is noise. A detached worktree falls back to its folder.</summary>
    public override string Title => Worktree.Branch ?? $"{Worktree.FolderName} · detached";

    /// <summary>The last worktree's lane line stops at its own dot.</summary>
    public bool IsLast { get; } = isLast;

    /// <summary>Its branch was deleted on origin, as when its pull request was merged; Clean up removes it.</summary>
    public bool IsGone => Worktree.BranchGone && !IsMissing;

    public override string Detail => IsGone ? "branch deleted on origin" : base.Detail;

    /// <summary>A worktree row is its branch already; only something to act on earns it a second line.</summary>
    public bool HasDetail => IsBroken || IsGone;
}
