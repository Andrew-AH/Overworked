using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Overworked.App.Views;

/// <summary>Small modals: heading, subject, message, then Cancel / action.</summary>
static class Dialog
{
    /// <summary>Asks the user to confirm an action.</summary>
    public static async Task<bool> Confirm(Window owner, string heading, string subject, string message, string action, bool danger)
    {
        var window = NewWindow(heading);
        var go = new Button { Content = action, IsDefault = true, Classes = { danger ? "danger" : "primary" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        go.Click += (_, _) => window.Close(true);
        cancel.Click += (_, _) => window.Close(false);

        window.Content = Layout(heading, subject, message, [], cancel, go);
        window.Opened += (_, _) => cancel.Focus();
        return await window.ShowDialog<bool?>(owner) == true;
    }

    /// <summary>Asks for one line of text, trimmed; null if cancelled.</summary>
    /// <param name="validate">Why the text won't do, or null when it will. The dialog stays open until it will.</param>
    public static async Task<string?> Prompt(Window owner, string heading, string subject, string message,
        string text, string placeholder, string action, Func<string, string?> validate)
    {
        var window = NewWindow(heading);
        var input = new TextBox { Text = text, PlaceholderText = placeholder, Classes = { "field" }, Margin = new Thickness(0, 14, 0, 0) };
        var problem = new TextBlock { Classes = { "danger" }, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), IsVisible = false };
        var go = new Button { Content = action, IsDefault = true, Classes = { "primary" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        go.Click += (_, _) =>
        {
            var answer = input.Text?.Trim() ?? "";
            problem.Text = validate(answer);
            problem.IsVisible = problem.Text is not null;
            if (problem.Text is null) window.Close(answer);
        };
        cancel.Click += (_, _) => window.Close(null);

        window.Content = Layout(heading, subject, message, [input, problem], cancel, go);
        window.Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        return await window.ShowDialog<string?>(owner);
    }

    static Window NewWindow(string title)
    {
        var window = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            CanMinimize = false,
            CanMaximize = false,
            ShowInTaskbar = false,
            ExtendClientAreaToDecorationsHint = true, // the heading below is the title
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        window.Bind(Window.BackgroundProperty, window.GetResourceObservable("Raised"));
        return window;
    }

    static StackPanel Layout(string heading, string subject, string message, Control[] fields, Button cancel, Button go)
    {
        var layout = new StackPanel
        {
            Margin = new Thickness(24, 28, 24, 20),
            Children =
            {
                new TextBlock { Text = heading, FontSize = 17, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = subject, Classes = { "mono" }, FontSize = 12, Margin = new Thickness(0, 3, 0, 14) },
                new TextBlock { Text = message, Classes = { "muted" }, FontSize = 12.5, TextWrapping = TextWrapping.Wrap },
            },
        };
        layout.Children.AddRange(fields);
        layout.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 22, 0, 0),
            Children = { cancel, go },
        });
        return layout;
    }
}
