namespace Overworked.Core;

/// <summary>What <see cref="Workspace.CleanUp"/> did.</summary>
/// <param name="BranchDeleted">False when the branch stayed: it may hold commits origin never got.</param>
/// <param name="FolderLeftBehind">The worktree's empty folder stayed because something still has it open.</param>
/// <param name="SwitchedTo">The branch the repo's own folder switched to, when the branch was checked out there.</param>
public sealed record CleanUpResult(bool BranchDeleted, bool FolderLeftBehind = false, string? SwitchedTo = null)
{
    /// <summary>For messages, e.g. "removed the worktree and deleted the branch".</summary>
    public string Describe() =>
        (SwitchedTo is { } to ? $"switched the main folder to {to}" : "removed the worktree")
        + (BranchDeleted ? " and deleted the branch" : ", but kept the branch: it may have commits origin never got")
        + (FolderLeftBehind ? ". Its empty folder goes once an editor or a terminal stops using it" : "");
}
