using System.Diagnostics;

namespace Overworked.App;

/// <summary>The only place that knows Windows from Mac.</summary>
static class Platform
{
    public static void OpenInCode(string path) =>
        Start(OperatingSystem.IsWindows() ? ["cmd", "/c", "code", path] : ["open", "-a", "Visual Studio Code", path]);

    public static void OpenFolder(string path) =>
        Start(OperatingSystem.IsWindows() ? ["explorer", path] : ["open", path]);

    public static void OpenTerminal(string path) =>
        Start(OperatingSystem.IsWindows() ? ["wt", "-d", path] : ["open", "-a", "Terminal", path]);

    static void Start(string[] command)
    {
        var psi = new ProcessStartInfo(command[0]) { CreateNoWindow = true };
        foreach (var arg in command[1..]) psi.ArgumentList.Add(arg);
        Process.Start(psi)?.Dispose();
    }
}
