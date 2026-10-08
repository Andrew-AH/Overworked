using Overworked.Core;

namespace Overworked.Cli;

// What `ow --json` prints. Scripts and Claude's skills parse it, so these shapes are a contract: add fields freely,
// but don't rename or remove any. Core's records are mapped here rather than serialised directly, so renaming
// something in Core can't change the output.

sealed record RepoJson(string Name, string Path, IReadOnlyList<WorktreeJson> Worktrees, string? Error)
{
    public static RepoJson From(Repo repo) =>
        new(repo.Name, repo.Path, repo.Worktrees.Select(WorktreeJson.From).ToList(), repo.Error);
}

/// <param name="Name">The worktree's folder name.</param>
/// <param name="Changes">Uncommitted changes.</param>
/// <param name="PullRequest">The pull request link its branch is tagged with.</param>
/// <param name="Ticket">The ticket link its branch is tagged with.</param>
/// <param name="BranchGone">Its branch was deleted on origin (known after a fetch), as when its PR was merged.</param>
sealed record WorktreeJson(
    string Path, string? Branch, bool IsMain, bool IsMissing, int Changes, string? PullRequest, string Name, string? Ticket,
    bool BranchGone)
{
    public static WorktreeJson From(Worktree worktree) => new(worktree.Path, worktree.Branch, worktree.IsMain,
        worktree.IsMissing, worktree.ChangedFiles, worktree.PullRequestUrl, worktree.FolderName, worktree.TicketUrl,
        worktree.BranchGone);
}

sealed record BranchJson(string Name, bool IsRemote, string? CheckedOutAt, DateTimeOffset LastCommit)
{
    public static BranchJson From(BranchRef branch) =>
        new(branch.Name, branch.IsRemote, branch.CheckedOutAt, branch.LastCommit);
}
