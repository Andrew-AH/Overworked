namespace Overworked.Core;

/// <summary>Work that removing a worktree would destroy (<see cref="Worktrees.FindWorkAtRisk"/>).</summary>
/// <param name="SubmodulesWithUnpushedCommits">
/// A linked worktree keeps its submodules' git data inside its own admin folder, so commits they never pushed are
/// deleted along with it.
/// </param>
public sealed record WorkAtRisk(int UncommittedChanges, IReadOnlyList<Submodule> SubmodulesWithUnpushedCommits)
{
    /// <summary>For messages, e.g. "2 uncommitted changes and 1 unpushed commit in infra".</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (UncommittedChanges > 0) parts.Add(Plural.Of(UncommittedChanges, "uncommitted change"));
        foreach (var submodule in SubmodulesWithUnpushedCommits)
            parts.Add($"{Plural.Of(submodule.UnpushedCommits, "unpushed commit")} in {submodule.Name}");
        return string.Join(" and ", parts);
    }
}
