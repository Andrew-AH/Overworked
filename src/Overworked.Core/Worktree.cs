namespace Overworked.Core;

/// <summary>A checkout of a repo: its main folder, or a linked worktree.</summary>
/// <param name="Branch">The checked-out branch; null on a detached HEAD.</param>
/// <param name="IsMain">The repo's own folder. git lists it first, and it can't be removed.</param>
/// <param name="IsMissing">git still lists it but its folder is gone; removing it cleans up.</param>
/// <param name="ChangedFiles">Uncommitted changes. Not counted for a missing worktree.</param>
/// <param name="PullRequestUrl">The pull request its branch is tagged with (<see cref="Tags.Set"/>).</param>
/// <param name="TicketUrl">The ticket its branch is tagged with.</param>
public sealed record Worktree(
    string Path, string? Branch, bool IsMain, bool IsMissing, int ChangedFiles = 0,
    string? PullRequestUrl = null, string? TicketUrl = null)
{
    public string FolderName => System.IO.Path.GetFileName(Path);

    /// <summary>Its ticket's key, e.g. PROJ-123; null when it has no ticket.</summary>
    public string? TicketKey => TicketUrl is null ? null : Tags.TicketKeyOf(TicketUrl);
}
