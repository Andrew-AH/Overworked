using Avalonia.Threading;

namespace Overworked.App;

/// <summary>
/// Raises <see cref="Changed"/>, on the UI thread and debounced, when anything adds, removes or switches a worktree in
/// the watched repos, including `ow` run by Claude, or tags a branch with a pull request.
/// </summary>
sealed class RepoWatcher : IDisposable
{
    readonly List<FileSystemWatcher> _watchers = [];
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(400) };
    string? _watching; // the repo paths being watched, joined, to skip re-watching the same set

    public RepoWatcher() => _debounce.Tick += (_, _) => { _debounce.Stop(); Changed?.Invoke(); };

    public event Action? Changed;

    /// <summary>Watches exactly these repos. A no-op when they're the ones already watched.</summary>
    public void Watch(IEnumerable<string> repoPaths)
    {
        var paths = repoPaths.ToList();
        var key = string.Join("|", paths);
        if (key == _watching) return;
        _watching = key;

        StopWatching();
        foreach (var path in paths) _watchers.Add(WatchGitFolder(path));
    }

    public void Dispose()
    {
        StopWatching();
        _debounce.Stop();
    }

    FileSystemWatcher WatchGitFolder(string repoPath)
    {
        var watcher = new FileSystemWatcher(Path.Combine(repoPath, ".git")) { IncludeSubdirectories = true };
        FileSystemEventHandler onChange = (_, e) =>
        {
            if (IsWorktreeChange(e.Name)) Dispatcher.UIThread.Post(() => { _debounce.Stop(); _debounce.Start(); });
        };
        watcher.Created += onChange;
        watcher.Deleted += onChange;
        watcher.Renamed += (sender, e) => onChange(sender, e);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    /// <summary>
    /// HEAD: a branch switched. worktrees/&lt;name&gt;: a worktree added or removed. config: `ow pr` tagged a branch.
    /// Not index or objects: our own `git status` touches those, and would loop.
    /// </summary>
    static bool IsWorktreeChange(string? gitPath) =>
        gitPath?.Split('/', '\\') is ["HEAD"] or ["config"] or ["worktrees"] or ["worktrees", _] or ["worktrees", _, "HEAD"];

    void StopWatching()
    {
        _watchers.ForEach(w => w.Dispose());
        _watchers.Clear();
    }
}
