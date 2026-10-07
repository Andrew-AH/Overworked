namespace Overworked.Core;

/// <summary>A main repo under the workspace root, with its worktrees, main first.</summary>
/// <param name="Name">Its folder name under the root.</param>
/// <param name="Error">Why git couldn't read it; <paramref name="Worktrees"/> is empty then.</param>
public sealed record Repo(string Name, string Path, IReadOnlyList<Worktree> Worktrees, string? Error = null)
{
    /// <summary>The repo's own checkout; null only when the repo couldn't be read.</summary>
    public Worktree? Main => Worktrees.FirstOrDefault();

    /// <summary>Its worktrees other than the main checkout.</summary>
    public IEnumerable<Worktree> Linked => Worktrees.Skip(1);

    // Records compare lists by reference. Compare the worktrees themselves, so a rescan that found nothing new
    // equals the last one.
    public bool Equals(Repo? other) =>
        other is not null && Name == other.Name && Path == other.Path && Error == other.Error
        && Worktrees.SequenceEqual(other.Worktrees);

    public override int GetHashCode() => HashCode.Combine(Name, Path, Error, Worktrees.Count);
}
