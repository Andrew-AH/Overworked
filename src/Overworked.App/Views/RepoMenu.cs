using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Overworked.App.ViewModels;
using Overworked.Core;

namespace Overworked.App.Views;

/// <summary>
/// Menus that drop down from a repo's name: its right-click menu (a "Switch branch" search above the usual actions), and
/// "New worktree" (the same search: pick a branch to open it as a worktree, or type a new name).
/// </summary>
static class RepoMenu
{
    const double MenuWidth = 300;

    /// <param name="anchor">The repo's name; the menu drops down from it, so it always lands inside the window.</param>
    /// <param name="main">The repo's main checkout, whose branch the search switches. Null leaves the search out.</param>
    public static void Show(Control anchor, string repoPath, Worktree? main,
        IReadOnlyList<(string Label, Action Run)> actions, Action<BranchChoice> onSwitch)
    {
        var (flyout, listHeight) = NewFlyout(anchor);
        var menu = new StackPanel { Width = MenuWidth };
        var actionList = new StackPanel();

        if (main is not null)
        {
            var search = new BranchPicker(flyout, "Switch branch", repoPath, exclude: main.Branch, listHeight, listOpen: false, onSwitch);
            // Once you're picking a branch the menu becomes just the picker, so it stays a sensible size.
            search.Searching += () => actionList.IsVisible = false;
            menu.Children.Add(search);
            actionList.Children.Add(new Border { Classes = { "menudivider" } });
        }
        foreach (var (label, run) in actions)
        {
            var item = new Button { Content = label, Classes = { "menuitem" } };
            item.Click += (_, _) => { flyout.Hide(); run(); };
            actionList.Children.Add(item);
        }
        menu.Children.Add(actionList);

        flyout.Content = menu;
        flyout.ShowAt(anchor);
    }

    /// <summary>
    /// Pick a branch to open as a worktree: an existing one (local or on origin) is checked out, a new name is created
    /// from origin/main. A branch already checked out somewhere is shown but can't be picked.
    /// </summary>
    public static void ShowNewWorktree(Control anchor, string repoPath, Action<BranchChoice> onPick)
    {
        var (flyout, listHeight) = NewFlyout(anchor);
        flyout.Content = new BranchPicker(flyout, "New worktree", repoPath, exclude: null, listHeight, listOpen: true, onPick)
        {
            Width = MenuWidth,
        };
        flyout.ShowAt(anchor);
    }

    /// <summary>Pick a repo from a list, e.g. one to open a ticket's branch in too.</summary>
    public static void ShowRepos(Control anchor, string heading, IReadOnlyList<Repo> repos, Action<Repo> onPick)
    {
        var (flyout, listHeight) = NewFlyout(anchor);
        var list = new StackPanel();
        foreach (var repo in repos)
        {
            var item = new Button { Content = repo.Name, Classes = { "menuitem" } };
            item.Click += (_, _) => { flyout.Hide(); onPick(repo); };
            list.Children.Add(item);
        }
        flyout.Content = new StackPanel
        {
            Width = MenuWidth,
            Children =
            {
                new TextBlock
                {
                    Text = heading, Classes = { "muted" }, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(8, 2, 0, 7),
                },
                new ScrollViewer { MaxHeight = listHeight, Content = list },
            },
        };
        flyout.ShowAt(anchor);
    }

    /// <summary>Opens towards whichever side of the window has more room, with a branch list sized to fit it.</summary>
    static (Flyout Flyout, double ListHeight) NewFlyout(Control anchor)
    {
        var window = TopLevel.GetTopLevel(anchor)!;
        var top = anchor.TranslatePoint(default, window)?.Y ?? 0;
        var below = window.Bounds.Height - top - anchor.Bounds.Height;
        var above = top - 40; // under the title bar
        var down = below >= above;

        var flyout = new Flyout
        {
            Placement = down ? PlacementMode.BottomEdgeAlignedLeft : PlacementMode.TopEdgeAlignedLeft,
            HorizontalOffset = -8,
            VerticalOffset = down ? 4 : -4,
        };
        flyout.FlyoutPresenterClasses.Add("picker");
        return (flyout, Math.Clamp((down ? below : above) - 100, 110, 250));
    }
}
