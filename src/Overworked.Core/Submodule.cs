namespace Overworked.Core;

/// <summary>An initialised submodule inside a worktree.</summary>
/// <param name="Name">Its path within the worktree, as the parent repo records it.</param>
/// <param name="Path">Its folder on disk.</param>
/// <param name="Branch">The checked-out branch; null on a detached HEAD, the usual state for a submodule.</param>
/// <param name="PointerMoved">Its commit isn't the one the parent repo records.</param>
/// <param name="UnpushedCommits">Commits on no remote branch.</param>
public sealed record Submodule(
    string Name, string Path, string? Branch, bool PointerMoved, int ChangedFiles, int UnpushedCommits);
