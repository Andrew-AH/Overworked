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

    /// <summary>A worktree row is its branch already; only a problem earns it a second line.</summary>
    public bool HasDetail => IsBroken;
}
