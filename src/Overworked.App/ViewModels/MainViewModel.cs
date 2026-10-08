using Overworked.Core;

namespace Overworked.App.ViewModels;

/// <summary>
/// The window's state: the repos folder, its repos as rows, and the status bar. Changes to repos run through here, and
/// the list follows changes made on disk, including by `ow`.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    readonly RepoWatcher _watcher = new();
    readonly HashSet<string> _collapsed = new(StringComparer.OrdinalIgnoreCase); // repos whose worktrees are hidden
    Workspace? _workspace = Config.LoadRoot() is { } root ? new Workspace(root) : null;
    IReadOnlyList<Repo> _repos = [];
    IReadOnlyList<RepoRow> _rows = [];
    string _summaryCount = "", _summaryRest = "";
    bool _mustRedraw = true; // redraw after the next scan even if nothing changed: the first scan, or a manual refresh
    bool _refreshing, _refreshAgain;
    int _edits;              // bumped by our own changes, so a full scan that started before one is known to be stale

    public MainViewModel() => _watcher.Changed += () => _ = RefreshAsync();

    public StatusViewModel Status { get; } = new();

    public bool HasRoot => _workspace is not null;
    public string RootName => _workspace is null ? "" : Path.GetFileName(_workspace.Root.TrimEnd(Path.DirectorySeparatorChar));
    public string RootTip => $"{_workspace?.Root}\nClick to choose another folder";

    public IReadOnlyList<RepoRow> Rows { get => _rows; private set => Set(ref _rows, value); }

    /// <summary>The summary line's emphasised start: "3 worktrees", or "Nothing".</summary>
    public string SummaryCount { get => _summaryCount; private set => Set(ref _summaryCount, value); }

    /// <summary>The rest of the summary line: " in flight across 12 repos".</summary>
    public string SummaryRest { get => _summaryRest; private set => Set(ref _summaryRest, value); }

    public async Task ChooseRootAsync(string folder)
    {
        Config.SaveRoot(folder);
        _workspace = new Workspace(Config.LoadRoot()!);
        OnPropertyChanged(nameof(HasRoot));
        OnPropertyChanged(nameof(RootName));
        OnPropertyChanged(nameof(RootTip));
        await RefreshAsync(redraw: true);
    }

    /// <summary>Rescans every repo. A request that arrives mid-scan runs once the current scan finishes.</summary>
    /// <param name="redraw">Redraw the list even if nothing changed.</param>
    public async Task RefreshAsync(bool redraw = false)
    {
        if (redraw) _mustRedraw = true;
        if (_refreshing) { _refreshAgain = true; return; }
        _refreshing = true;
        try
        {
            do
            {
                _refreshAgain = false;
                if (_workspace is not { } workspace) return;
                var edits = _edits;
                var repos = await Task.Run(workspace.LoadRepos);
                // We changed something while this scan ran, so parts of it predate that change: scan again.
                if (edits != _edits) { _refreshAgain = true; continue; }
                Show(repos);
            }
            while (_refreshAgain);
        }
        catch (Exception e)
        {
            Status.ShowError(e.Message);
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Opens a branch as a worktree: an existing branch is checked out, a new name branches from origin/main.</summary>
    public Task CreateWorktreeAsync(RepoRow row, string branch) =>
        ChangeRepoAsync(row.Repo, $"Opening {branch} as a worktree…", workspace =>
        {
            workspace.CreateWorktree(row.Repo, branch);
            return $"Opened {branch} as a worktree";
        });

    /// <summary>The work removing this worktree would destroy. None if its folder is already gone.</summary>
    public Task<WorkAtRisk?> FindWorkAtRiskAsync(WorktreeRow row) =>
        row.IsMissing ? Task.FromResult<WorkAtRisk?>(null) : Task.Run(() => Worktrees.FindWorkAtRisk(row.Path));

    /// <param name="force">Remove even though work would be lost; only after the user has confirmed that.</param>
    public Task RemoveWorktreeAsync(WorktreeRow row, bool force) =>
        ChangeRepoAsync(row.Repo, $"Removing {row.Title}…", workspace =>
            workspace.RemoveWorktree(row.Repo, row.Worktree, force)
                ? $"Removed the {row.Title} worktree"
                : $"Removed the {row.Title} worktree. Its empty folder goes once VS Code or a terminal stops using it.");

    /// <summary>Changes the main folder's branch. Worktrees don't switch: they stay on the branch they were made for.</summary>
    public Task SwitchBranchAsync(RepoRow row, BranchChoice choice)
    {
        if (row.Worktree is not { } main) return Task.CompletedTask;
        return ChangeRepoAsync(row.Repo, $"Switching {row.Name} to {choice.Name}…", _ =>
        {
            Branches.Switch(main.Path, choice.Name, choice.Create);
            return $"{row.Name} is now on {choice.Name}";
        });
    }

    /// <summary>Tags the row's branch with its ticket or pull request. Rows tagged with the same ticket share its colour.</summary>
    public Task TagAsync(RowViewModel row, TagKind kind, string url)
    {
        if (row.Branch is not { } branch) return Task.CompletedTask;
        var tag = kind == TagKind.Ticket ? Tags.TicketKeyOf(url) : "its pull request";
        return ChangeRepoAsync(row.Repo, $"Tagging {branch}…", _ =>
        {
            Tags.Set(row.Repo.Path, branch, kind, url);
            return $"Tagged {branch} with {tag}";
        });
    }

    public void ToggleExpanded(RepoRow row)
    {
        row.IsExpanded = !row.IsExpanded;
        if (row.IsExpanded) _collapsed.Remove(row.Path);
        else _collapsed.Add(row.Path);
    }

    public void Dispose() => _watcher.Dispose();

    /// <summary>
    /// Runs a change to one repo off the UI thread, rescans that repo, and only then reports the change done, so the list
    /// and the message never disagree.
    /// </summary>
    /// <param name="change">Makes the change and returns the message saying it's done.</param>
    async Task ChangeRepoAsync(Repo repo, string progress, Func<Workspace, string> change)
    {
        if (_workspace is not { } workspace) return;
        Status.ShowProgress(progress);
        try
        {
            var done = await Task.Run(() => change(workspace));
            await ReloadAsync(workspace, repo);
            Status.Show(done);
        }
        catch (Exception e)
        {
            Status.ShowError(e.Message);
            await ReloadAsync(workspace, repo);
        }
    }

    /// <summary>Rescans one repo after we changed it, so its row is right straight away; a full scan takes seconds.</summary>
    async Task ReloadAsync(Workspace workspace, Repo repo)
    {
        _edits++;
        var reloaded = await Task.Run(() => workspace.LoadRepo(repo.Name));
        Show(_repos.Select(r => string.Equals(r.Path, reloaded.Path, StringComparison.OrdinalIgnoreCase) ? reloaded : r).ToList());
    }

    void Show(IReadOnlyList<Repo> repos)
    {
        var changed = _mustRedraw || !repos.SequenceEqual(_repos);
        _repos = repos;
        _watcher.Watch(repos.Select(r => r.Path));
        if (!changed) return;

        _mustRedraw = false;
        var colours = RowColours.For(repos);
        Rows = InFlightFirst(repos).Select(r => new RepoRow(r, isExpanded: !_collapsed.Contains(r.Path), colours)).ToList();
        var worktrees = repos.Sum(r => r.Linked.Count(w => !w.IsMissing));
        SummaryCount = worktrees == 0 ? "Nothing" : Plural.Of(worktrees, "worktree");
        SummaryRest = $" in flight across {Plural.Of(repos.Count, "repo")}";
    }

    /// <summary>Repos with worktrees first, so the work in flight sits at the top; each group keeps its order.</summary>
    internal static IEnumerable<Repo> InFlightFirst(IEnumerable<Repo> repos) => repos.OrderBy(r => r.Linked.Any() ? 0 : 1);
}
