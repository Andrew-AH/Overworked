namespace Overworked.Core;

/// <summary>A branch you could switch to or open as a worktree.</summary>
/// <param name="IsRemote">Only on origin so far; checking it out makes a local branch that tracks it.</param>
/// <param name="CheckedOutAt">The folder it's checked out in, if any. git allows one place per branch.</param>
/// <param name="LastCommit">When its newest commit was made.</param>
public sealed record BranchRef(string Name, bool IsRemote, string? CheckedOutAt, DateTimeOffset LastCommit);
