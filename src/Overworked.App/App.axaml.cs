using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Overworked.App.Views;
using Overworked.Core;

namespace Overworked.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // So `ow app` (and Claude) can open the app from a terminal. Dev builds (under bin\) don't count,
        // or running one would point `ow app` away from the real app.
        var sep = Path.DirectorySeparatorChar;
        if (Environment.ProcessPath is { } exe && !exe.Contains($"{sep}bin{sep}")) Config.SaveAppPath(exe);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
