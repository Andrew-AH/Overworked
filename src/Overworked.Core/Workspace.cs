namespace Overworked.Core;

/// <summary>
/// The folder that holds your repos. Every direct subfolder with a .git directory is a main repo; worktrees made here
/// go in &lt;root&gt;/.worktrees/&lt;repo&gt;/&lt;branch&gt;.
/// </summary>
public sealed class Workspace(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    string WorktreesFolder => Path.Combine(Root, ".worktrees");

    /// <summary>Every main repo, with its worktrees. Also tidies away worktree folders left behind earlier.</summary>
    public IReadOnlyList<Repo> LoadRepos()
    {
        Tidy();
        return Directory.EnumerateDirectories(Root)
            .Where(IsMainRepo)
            .Order(StringComparer.OrdinalIgnoreCase)
            .AsParallel().AsOrdered()
            .Select(LoadRepoAt)
            .ToList();
    }

    /// <summary>One repo by folder name. If git can't read it, that's reported in <see cref="Repo.Error"/>.</summary>
    public Repo LoadRepo(string name) => LoadRepoAt(Path.Combine(Root, name));

    /// <summary>One repo by folder name, for commands that can't go on without it.</summary>
    /// <exception cref="OverworkedException">There's no such repo, or git can't read it.</exception>
    public Repo FindRepo(string name)
    {
        var path = Path.Combine(Root, name);
        if (!IsMainRepo(path)) throw new OverworkedException($"No repo '{name}' in {Root}.");
        var repo = LoadRepoAt(path);
        return repo.Error is null ? repo : throw new OverworkedException(repo.Error);
    }

    /// <summary>
    /// Opens <paramref name="branch"/> as a worktree and initialises its submodules. An existing branch (local, or on
    /// origin, which git turns into a tracking branch) is checked out; a new name becomes a branch from origin/main.
    /// </summary>
    public Worktree CreateWorktree(Repo repo, string branch)
    {
        branch = Branches.RequireName(branch);
        var path = WorktreePath(repo.Name, branch);

        Git.Run(repo.Path, "fetch", "origin");
        if (Branches.Exists(repo.Path, branch))
            Git.Run(repo.Path, "worktree", "add", path, branch);
        else
            Git.Run(repo.Path, "worktree", "add", "--no-track", "-b", branch, path, Branches.Base(repo.Path));

        Submodules.Init(path);
        return new Worktree(path, branch, IsMain: false, IsMissing: false);
    }

    /// <summary>
    /// Removes a linked worktree; its branch is kept. Unless <paramref name="force"/> is set, refuses when that would
    /// destroy work (<see cref="Worktrees.FindWorkAtRisk"/>).
    /// </summary>
    /// <returns>
    /// False when the work was removed but the now-empty folder stayed because something still has it open (on
    /// Windows, VS Code or a terminal sitting in it). <see cref="Tidy"/> deletes it once it's let go.
    /// </returns>
    /// <exception cref="OverworkedException">It's the main worktree, it has work at risk, or git failed.</exception>
    public bool RemoveWorktree(Repo repo, Worktree worktree, bool force)
    {
        if (worktree.IsMain) throw new OverworkedException("The main worktree can't be removed.");

        if (!worktree.IsMissing)
        {
            if (!force && Worktrees.FindWorkAtRisk(worktree.Path) is { } atRisk)
                throw new OverworkedException($"{worktree.FolderName} has {atRisk.Describe()}. Force to discard them.");
            // --force because git refuses worktrees with submodules otherwise; the check above is the guard.
            try { Git.Run(repo.Path, "worktree", "remove", "--force", worktree.Path); }
            // git empties the folder and forgets the worktree, then can't delete a folder another process is in.
            catch (OverworkedException) when (IsEmpty(worktree.Path)) { }
        }
        Git.Run(repo.Path, "worktree", "prune");
        Tidy();
        return !Directory.Exists(worktree.Path);
    }

    /// <summary>
    /// Cleans up after a worktree whose branch was deleted on origin, as when its pull request was squash-merged:
    /// removes the worktree (refusing, like <see cref="RemoveWorktree"/>, when that would lose work), then deletes the
    /// local branch if origin had every commit on it. If there's anything newer, or no way to tell, the branch stays.
    /// </summary>
    /// <exception cref="OverworkedException">Its branch is still on origin, it has work at risk, or git failed.</exception>
    public CleanUpResult CleanUp(Repo repo, Worktree worktree)
    {
        if (worktree is not { BranchGone: true, Branch: { } branch })
            throw new OverworkedException($"{worktree.FolderName}'s branch is still on origin, so there's nothing to clean up.");
        var folderRemoved = RemoveWorktree(repo, worktree, force: false);
        var branchDeleted = Branches.WasFullyPushed(repo.Path, branch);
        if (branchDeleted) Branches.Delete(repo.Path, branch);
        return new CleanUpResult(folderRemoved, branchDeleted);
    }

    /// <summary>Deletes empty folders under &lt;root&gt;/.worktrees: leftovers of worktrees removed while in use.</summary>
    public void Tidy()
    {
        if (!Directory.Exists(WorktreesFolder)) return;
        foreach (var repoFolder in Directory.EnumerateDirectories(WorktreesFolder))
        {
            foreach (var worktreeFolder in Directory.EnumerateDirectories(repoFolder)) DeleteIfEmpty(worktreeFolder);
            DeleteIfEmpty(repoFolder);
        }
        DeleteIfEmpty(WorktreesFolder);
    }

    /// <summary>Slashes become dashes, so feature/x is one folder rather than two.</summary>
    string WorktreePath(string repoName, string branch) =>
        Path.Combine(WorktreesFolder, repoName, branch.Replace('/', '-').Replace('\\', '-'));

    // A linked worktree has a .git *file*; only a main repo has a .git directory.
    static bool IsMainRepo(string folder) => Directory.Exists(Path.Combine(folder, ".git"));

    static Repo LoadRepoAt(string path)
    {
        var name = Path.GetFileName(path);
        try
        {
            var tags = Tags.ByBranch(path);
            var gone = Branches.Gone(path);
            var worktrees = Worktrees.List(path)
                .AsParallel().AsOrdered()
                .Select(w =>
                {
                    var tagged = w.Branch is null ? default : tags.GetValueOrDefault(w.Branch);
                    return w with
                    {
                        ChangedFiles = w.IsMissing ? 0 : Worktrees.CountChangedFiles(w.Path),
                        PullRequestUrl = tagged.PullRequestUrl,
                        TicketUrl = tagged.TicketUrl,
                        BranchGone = w.Branch is not null && gone.Contains(w.Branch),
                    };
                })
                .ToList();
            return new Repo(name, path, worktrees);
        }
        catch (OverworkedException e)
        {
            return new Repo(name, path, [], e.Message);
        }
        // The same, from a worktree read inside the parallel query, which wraps it.
        catch (AggregateException e) when (e.InnerException is OverworkedException inner)
        {
            return new Repo(name, path, [], inner.Message);
        }
    }

    static bool IsEmpty(string folder) => !Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any();

    static void DeleteIfEmpty(string folder)
    {
        try { if (IsEmpty(folder)) Directory.Delete(folder); }
        catch (IOException) { }                  // still open somewhere; the next tidy gets it
        catch (UnauthorizedAccessException) { }
    }
}
