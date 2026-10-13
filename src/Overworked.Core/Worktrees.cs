namespace Overworked.Core;

/// <summary>Reading a repo's worktrees and what's in them.</summary>
public static class Worktrees
{
    /// <summary>The work that removing this worktree would destroy, or null when there is none.</summary>
    public static WorkAtRisk? FindWorkAtRisk(string worktreePath)
    {
        var changes = CountChangedFiles(worktreePath);
        var submodules = Submodules.List(worktreePath).Where(s => s.ChangedFiles > 0 || s.UnpushedCommits > 0).ToList();
        return changes == 0 && submodules.Count == 0 ? null : new WorkAtRisk(changes, submodules);
    }

    /// <summary>A repo's worktrees, main first, as git lists them: no change counts or pull requests yet.</summary>
    internal static IReadOnlyList<Worktree> List(string repoPath) =>
        Parse(Git.Run(repoPath, "worktree", "list", "--porcelain"));

    /// <summary>
    /// Uncommitted changes: each line of `git status --porcelain` is one changed path. A submodule counts when its commit
    /// moved, not for changes inside it: finding those takes a second git process per submodule, which the removal guard
    /// spends (<see cref="FindWorkAtRisk"/>) and a scan needn't.
    /// </summary>
    internal static int CountChangedFiles(string worktreePath) =>
        Git.Lines(Git.Run(worktreePath, "status", "--porcelain", "--ignore-submodules=dirty")).Length;

    /// <summary>`git worktree list --porcelain`: a block per worktree, separated by blank lines, main first.</summary>
    internal static IReadOnlyList<Worktree> Parse(string porcelain)
    {
        var worktrees = new List<Worktree>();
        foreach (var block in porcelain.ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? path = null, branch = null;
            var missing = false;
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("worktree ")) path = Path.GetFullPath(line["worktree ".Length..]);
                else if (line.StartsWith("branch refs/heads/")) branch = line["branch refs/heads/".Length..];
                else if (line.StartsWith("prunable")) missing = true;
            }
            if (path is not null) worktrees.Add(new Worktree(path, branch, IsMain: worktrees.Count == 0, IsMissing: missing));
        }
        return worktrees;
    }
}
