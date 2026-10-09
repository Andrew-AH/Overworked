namespace Overworked.Core;

/// <summary>A checkout of a repo: its main folder, or a linked worktree.</summary>
/// <param name="Branch">The checked-out branch; null on a detached HEAD.</param>
/// <param name="IsMain">The repo's own folder. git lists it first, and it can't be removed.</param>
/// <param name="IsMissing">git still lists it but its folder is gone; removing it cleans up.</param>
/// <param name="ChangedFiles">Uncommitted changes. Not counted for a missing worktree.</param>
/// <param name="PullRequestUrl">The pull request its branch is tagged with (<see cref="Tags.Set"/>).</param>
/// <param name="TicketUrl">The ticket its branch is tagged with.</param>
/// <param name="BranchGone">Its branch was deleted on origin, as when its pull request was merged (<see cref="Workspace.CleanUp"/>).</param>
/// <param name="UnpushedCommits">Commits on its branch that origin doesn't have yet (<see cref="Branches.CountUnpushed"/>).</param>
public sealed record Worktree(
    string Path, string? Branch, bool IsMain, bool IsMissing, int ChangedFiles = 0,
    string? PullRequestUrl = null, string? TicketUrl = null, bool BranchGone = false, int UnpushedCommits = 0)
{
    public string FolderName => System.IO.Path.GetFileName(Path);

    /// <summary>
    /// Its ticket's key, e.g. PROJ-123; null when it has no ticket. "Ticket" for a link that names none, which only a
    /// hand-edited git config can hold.
    /// </summary>
    public string? TicketKey => TicketUrl is null ? null : Tags.TicketKeyOf(TicketUrl) ?? "Ticket";
}
