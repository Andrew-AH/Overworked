using Avalonia;
using Overworked.Core;

namespace Overworked.App;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Homebrew.AddToPath();
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
