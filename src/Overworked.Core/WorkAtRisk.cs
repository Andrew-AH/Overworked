namespace Overworked.Core;

/// <summary>Work that removing a worktree would destroy (<see cref="Worktrees.FindWorkAtRisk"/>).</summary>
/// <param name="UncommittedChanges">The worktree's own, not counting changes inside its submodules.</param>
/// <param name="SubmodulesWithWork">
/// Submodules with uncommitted changes or unpushed commits. A linked worktree keeps its submodules' git data inside its
/// own admin folder, so commits they never pushed are deleted along with it.
/// </param>
public sealed record WorkAtRisk(int UncommittedChanges, IReadOnlyList<Submodule> SubmodulesWithWork)
{
    /// <summary>For messages, e.g. "2 uncommitted changes and 1 unpushed commit in infra".</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (UncommittedChanges > 0) parts.Add(Plural.Of(UncommittedChanges, "uncommitted change"));
        foreach (var submodule in SubmodulesWithWork)
        {
            if (submodule.ChangedFiles > 0) parts.Add($"{Plural.Of(submodule.ChangedFiles, "uncommitted change")} in {submodule.Name}");
            if (submodule.UnpushedCommits > 0) parts.Add($"{Plural.Of(submodule.UnpushedCommits, "unpushed commit")} in {submodule.Name}");
        }
        return string.Join(" and ", parts);
    }
}
