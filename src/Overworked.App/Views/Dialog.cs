using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Overworked.App.Views;

/// <summary>A small confirmation modal: heading, subject, message, Cancel / action.</summary>
static class Dialog
{
    public static async Task<bool> Confirm(Window owner, string heading, string subject, string message, string action, bool danger)
    {
        var window = new Window
        {
            Title = heading,
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

        var go = new Button { Content = action, IsDefault = true, Classes = { danger ? "danger" : "primary" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        go.Click += (_, _) => window.Close(true);
        cancel.Click += (_, _) => window.Close(false);

        window.Content = new StackPanel
        {
            Margin = new Thickness(24, 28, 24, 20),
            Children =
            {
                new TextBlock { Text = heading, FontSize = 17, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = subject, Classes = { "mono" }, FontSize = 12, Margin = new Thickness(0, 3, 0, 14) },
                new TextBlock { Text = message, Classes = { "muted" }, FontSize = 12.5, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 22, 0, 0),
                    Children = { cancel, go },
                },
            },
        };
        window.Opened += (_, _) => cancel.Focus();
        return await window.ShowDialog<bool?>(owner) == true;
    }
}
