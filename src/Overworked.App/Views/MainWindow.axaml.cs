using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Overworked.App.ViewModels;
using Overworked.Core;

namespace Overworked.App.Views;

/// <summary>
/// The window. Its state, and every change to a repo, lives in <see cref="MainViewModel"/>; this does what only a view
/// can: menus, dialogs, the folder picker, the clipboard, and opening things in other apps.
/// </summary>
public partial class MainWindow : Window
{
    readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        // Leave room for the traffic lights.
        if (OperatingSystem.IsMacOS()) Brand.Margin = new Thickness(80, 0, 0, 0);
        // A real title bar to the OS: drag, double-click to maximise/restore, snap layouts.
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(TitleBar, WindowDecorationsElementRole.TitleBar);

        RepoList.AddHandler(DoubleTappedEvent, Row_DoubleTapped, handledEventsToo: true);
        // Opening the app checks origin for branches deleted there; coming back to it rescans. Both also happen every minute.
        Opened += (_, _) => _ = _viewModel.RefreshAsync(fetch: true);
        Activated += (_, _) => _ = _viewModel.RefreshAsync();
        Closed += (_, _) => _viewModel.Dispose();
    }

    // Header

    async void ChooseRoot_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose the folder that holds your repos" });
        if (folders is [var folder] && folder.TryGetLocalPath() is { } path) await _viewModel.ChooseRootAsync(path);
    }

    async void Refresh_Click(object? sender, RoutedEventArgs e) => await _viewModel.RefreshAsync(redraw: true, fetch: true);

    // Repo rows

    /// <summary>The repo menu. Branch switching lives here only: worktrees stay on the branch they were made for.</summary>
    void RepoRow_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Border { DataContext: RepoRow row } border) return;
        e.Handled = true;
        var anchor = RepoNameIn(border);
        RepoMenu.Show(anchor, row.Repo.Path, row.Worktree,
        [
            ("New worktree…", () => NewWorktree(row, anchor)),
            ($"Open in {Platform.EditorName}", () => Open(Platform.OpenInCode, row)),
            .. row.HasBranch
                ? new[]
                {
                    ("Copy branch name", (Action)(() => CopyBranch(row))),
                    ("Tag ticket…", () => TagBranch(row, TagKind.Ticket)),
                    ("Tag pull request…", () => TagBranch(row, TagKind.PullRequest)),
                }
                : [],
            .. row.HasTicket
                ? new[]
                {
                    (row.OpenTicketText, (Action)(() => OpenTicketInCode(row))),
                    (row.AddRepoToTicketText, () => AddRepoToTicket(row, anchor)),
                }
                : [],
            .. row.CanCleanUp ? new[] { ("Clean up", (Action)(() => _ = _viewModel.CleanUpAsync(row))) } : [],
        ],
        async choice => await _viewModel.SwitchBranchAsync(row, choice));
    }

    void NewWorktree_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Visual button && RowOf<RepoRow>(sender) is { } row) NewWorktree(row, RepoNameIn(RowBorderOf(button)));
    }

    /// <summary>Pick any branch to open it as a worktree, or type a new name to create one from origin's default branch.</summary>
    void NewWorktree(RepoRow row, Control anchor) =>
        RepoMenu.ShowNewWorktree(anchor, row.Repo.Path, async choice => await _viewModel.CreateWorktreeAsync(row, choice.Name));

    void Toggle_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf<RepoRow>(sender) is { } row) _viewModel.ToggleExpanded(row);
    }

    // Worktree rows

    async void RemoveWorktree_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf<WorktreeRow>(sender) is not { } row) return;
        var atRisk = await _viewModel.FindWorkAtRiskAsync(row);
        var kept = row.HasBranch ? " The branch is kept." : "";
        var confirmed = atRisk is not null
            ? await Dialog.Confirm(this, "Remove worktree", row.Title,
                $"This worktree has {atRisk.Describe()}. Removing the worktree deletes that work.{kept}", "Delete work and remove", danger: true)
            : await Dialog.Confirm(this, "Remove worktree", row.Title, $"Deletes its folder.{kept}", "Remove", danger: true);
        if (confirmed) await _viewModel.RemoveWorktreeAsync(row, force: atRisk is not null);
    }

    void AddRepoToTicket_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf<RowViewModel>(sender) is { } row) AddRepoToTicket(row, RowBorderFor(row));
    }

    // Any row

    async void CleanUp_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf<RowViewModel>(sender) is { } row) await _viewModel.CleanUpAsync(row);
    }

    void OpenTicketInCode_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf<RowViewModel>(sender) is { } row) OpenTicketInCode(row);
    }

    /// <summary>Every checkout tagged with the row's ticket, across repos, in one VS Code window.</summary>
    void OpenTicketInCode(RowViewModel row)
    {
        if (row.TicketKey is not { } key) return;
        try { Platform.OpenInCode(_viewModel.TicketFolders(key)); }
        catch (Exception e) { _viewModel.Status.ShowError(e.Message); }
    }

    /// <summary>Pick another repo to open the row's branch in too, tagged with the same ticket.</summary>
    void AddRepoToTicket(RowViewModel row, Control anchor)
    {
        if (row.Branch is not { } branch) return;
        var repos = _viewModel.ReposWithout(branch);
        if (repos.Count == 0) _viewModel.Status.Show($"Every repo already has {branch} checked out");
        else RepoMenu.ShowRepos(anchor, $"Open {branch} in", repos, async other => await _viewModel.AddRepoToTicketAsync(row, other));
    }

    void Row_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-clicking a row's own buttons (+, chevron) shouldn't also open it.
        if ((e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is null) Open(Platform.OpenInCode, RowOf<RowViewModel>(e.Source));
    }

    async void OpenPullRequest_Click(object? sender, RoutedEventArgs e) => await OpenLink(RowOf<RowViewModel>(sender)?.PullRequestUrl);

    async void OpenTicket_Click(object? sender, RoutedEventArgs e) => await OpenLink(RowOf<RowViewModel>(sender)?.TicketUrl);

    async Task OpenLink(string? link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var url)) await Launcher.LaunchUriAsync(url);
    }

    void OpenInCode_Click(object? sender, RoutedEventArgs e) => Open(Platform.OpenInCode, RowOf<RowViewModel>(sender));

    void CopyBranch_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf<RowViewModel>(sender) is { } row) CopyBranch(row);
    }

    void TagTicket_Click(object? sender, RoutedEventArgs e) => TagBranch(RowOf<RowViewModel>(sender), TagKind.Ticket);
    void TagPullRequest_Click(object? sender, RoutedEventArgs e) => TagBranch(RowOf<RowViewModel>(sender), TagKind.PullRequest);

    /// <summary>Asks for the link to tag the row's branch with, showing the one it has now, or for a ticket, the one its name suggests.</summary>
    async void TagBranch(RowViewModel? row, TagKind kind)
    {
        if (row?.Branch is not { } branch) return;
        var ticket = kind == TagKind.Ticket;
        var link = await Dialog.Prompt(this,
            heading: ticket ? "Tag ticket" : "Tag pull request",
            subject: branch,
            message: ticket
                ? "Paste the ticket's link: its own page, or a board with it open. Every row with the same ticket shares its colour."
                : "Paste the pull request's link.",
            text: (ticket ? row.TicketUrl ?? _viewModel.SuggestTicketUrl(branch) : row.PullRequestUrl) ?? "",
            placeholder: "https://…",
            action: "Tag",
            validate: answer => answer.Length == 0 ? "Paste a link first." : Tags.ProblemWith(kind, answer));
        if (link is not null) await _viewModel.TagAsync(row, kind, link);
    }

    void CopyBranch(RowViewModel row) => Copy(row.Branch);

    // Right-clicking a ticket or PR pill.
    void CopyTicketLink_Click(object? sender, RoutedEventArgs e) => Copy(RowOf<RowViewModel>(sender)?.TicketUrl);
    void CopyPullRequestLink_Click(object? sender, RoutedEventArgs e) => Copy(RowOf<RowViewModel>(sender)?.PullRequestUrl);

    async void Copy(string? text)
    {
        if (text is null || Clipboard is null) return;
        await Clipboard.SetTextAsync(text);
        _viewModel.Status.Show($"Copied {text}");
    }

    void Open(Action<string> open, RowViewModel? row)
    {
        if (row is null) return;
        try { open(row.Path); }
        catch (Exception e) { _viewModel.Status.ShowError(e.Message); }
    }

    /// <summary>The row an element belongs to, from its data context.</summary>
    static T? RowOf<T>(object? element) where T : RowViewModel => (element as StyledElement)?.DataContext as T;

    /// <summary>The row's border around a control inside it.</summary>
    static Border RowBorderOf(Visual element) => element.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("row"));

    /// <summary>A row's border, found from the row: for a context-menu item, which sits outside the row.</summary>
    Border RowBorderFor(RowViewModel row) =>
        RepoList.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("row") && b.DataContext == row);

    /// <summary>The repo's name in its row; repo menus drop down from it.</summary>
    static TextBlock RepoNameIn(Visual row) => row.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("reponame"));
}
