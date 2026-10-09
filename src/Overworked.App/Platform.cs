using System.Diagnostics;

namespace Overworked.App;

/// <summary>The only place that knows Windows from Mac.</summary>
static class Platform
{
    public static void OpenInCode(string path) => OpenInCode([path]);

    /// <summary>Several folders open together in one window, as a multi-root workspace.</summary>
    public static void OpenInCode(IReadOnlyList<string> paths) =>
        Start(OperatingSystem.IsWindows() ? ["cmd", "/c", "code", .. paths] : ["open", "-a", "Visual Studio Code", .. paths]);

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
