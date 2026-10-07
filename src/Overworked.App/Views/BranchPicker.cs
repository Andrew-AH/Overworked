using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Overworked.App.ViewModels;
using Overworked.Core;

namespace Overworked.App.Views;

/// <summary>
/// A branch search inside a flyout. Click into the box to list every branch, or type to filter (VS Code style);
/// ↑/↓ and Enter pick one, Esc closes. Lists branches as soon as the flyout opens, then fetches origin and lists again.
/// </summary>
sealed class BranchPicker : StackPanel
{
    readonly Flyout _flyout;
    readonly string _repoPath;
    readonly string? _exclude;
    readonly Action<BranchChoice> _onPick;
    readonly TextBox _search = new() { Classes = { "search" }, PlaceholderText = "Search branches" };
    readonly ListBox _list;
    readonly TextBlock _status = new() { Classes = { "mono" }, FontSize = 11, Margin = new Thickness(8, 6, 0, 0), IsVisible = false };
    IReadOnlyList<BranchRef>? _branches; // null until the first read finishes

    /// <param name="exclude">A branch to leave out: the one already checked out, when switching.</param>
    /// <param name="listOpen">Show the list straight away, rather than once you click into the box.</param>
    public BranchPicker(Flyout flyout, string heading, string repoPath, string? exclude, double listHeight, bool listOpen,
        Action<BranchChoice> onPick)
    {
        _flyout = flyout;
        _repoPath = repoPath;
        _exclude = exclude;
        _onPick = onPick;
        _list = new ListBox
        {
            Classes = { "branches" },
            MaxHeight = listHeight,
            Margin = new Thickness(0, 6, 0, 0),
            IsVisible = false,
            // Avalonia hands the template null while it recycles rows, so build nothing for it.
            ItemTemplate = new FuncDataTemplate<BranchOption?>((option, _) => option is null ? new Panel() : Row(option)),
        };

        _search.AddHandler(PointerPressedEvent, (_, _) => ShowList(), RoutingStrategies.Tunnel);
        _search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        _search.TextChanged += (_, _) => { if (_list.IsVisible) Refilter(); else ShowList(); };
        _list.Tapped += (_, _) => Choose();
        flyout.Opened += LoadBranches;

        if (listOpen)
        {
            _list.IsVisible = true;
            Refilter(); // "loading branches…" until they arrive
            flyout.Opened += (_, _) => Dispatcher.UIThread.Post(() => _search.Focus(), DispatcherPriority.Loaded);
        }

        Children.Add(new TextBlock
        {
            Text = heading, Classes = { "muted" }, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(8, 2, 0, 7),
        });
        Children.Add(_search);
        Children.Add(_list);
        Children.Add(_status);
    }

    /// <summary>Raised when the list first opens, so the menu around the picker can make room for it.</summary>
    public event Action? Searching;

    protected override Type StyleKeyOverride => typeof(StackPanel);

    async void LoadBranches(object? sender, EventArgs e)
    {
        try
        {
            _branches = await Task.Run(() => Branches.List(_repoPath));
            Refilter();
            ShowStatus("fetching origin…");
            await Task.Run(() => Branches.Fetch(_repoPath));
            _branches = await Task.Run(() => Branches.List(_repoPath));
            Refilter();
            ShowStatus("");
        }
        catch (OverworkedException ex)
        {
            ShowStatus($"couldn't fetch origin: {ex.Message.Split('\n')[0]}");
        }
    }

    /// <summary>
    /// The list stays folded away until you click into the box (every branch) or start typing (filtered), so the menu
    /// stays short when you only want an action below it.
    /// </summary>
    void ShowList()
    {
        if (_list.IsVisible) return;
        Searching?.Invoke();
        _list.IsVisible = true;
        ShowStatus(_status.Text ?? "");
        Refilter();
    }

    void Refilter()
    {
        if (!_list.IsVisible) return;
        // Typed before the branches arrived: offer nothing yet, not even "Create" (it might already exist).
        if (_branches is null)
        {
            _list.ItemsSource = null;
            ShowStatus("loading branches…");
            return;
        }
        var query = _search.Text?.Trim() ?? "";
        var options = BranchSearch.Filter(_branches, query, _exclude, mainFolder: _repoPath, DateTimeOffset.Now);
        _list.ItemsSource = options;
        _list.SelectedIndex = query.Length > 0 ? options.FindIndex(o => o.CanPick) : -1;
    }

    /// <summary>The status line under the list; shown only along with the list.</summary>
    void ShowStatus(string text)
    {
        _status.Text = text;
        _status.IsVisible = _list.IsVisible && text.Length > 0;
    }

    void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when !_list.IsVisible: ShowList(); break;
            case Key.Down: Move(1); break;
            case Key.Up: Move(-1); break;
            case Key.Enter: Choose(); break;
            case Key.Escape: _flyout.Hide(); break;
            default: return;
        }
        e.Handled = true;
    }

    void Move(int step)
    {
        if (_list.ItemCount == 0) return;
        _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + step, 0, _list.ItemCount - 1);
        if (_list.SelectedItem is { } selected) _list.ScrollIntoView(selected);
    }

    void Choose()
    {
        if (_list.SelectedItem is not BranchOption { CanPick: true } option) return;
        _flyout.Hide();
        _onPick(option.Choice);
    }

    static Control Row(BranchOption option)
    {
        var tag = new TextBlock
        {
            Text = option.Tag, Classes = { "branchtag" }, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(tag, 1);
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Opacity = option.CanPick ? 1 : 0.5,
            Children =
            {
                new TextBlock { Text = option.Label, Classes = { "branchname" }, TextTrimming = TextTrimming.CharacterEllipsis },
                tag,
            },
        };
        if (option.Tip is not null) ToolTip.SetTip(row, option.Tip);
        return row;
    }
}
