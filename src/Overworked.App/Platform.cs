using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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

    /// <summary>The open VS Code windows and their titles.</summary>
    // ponytail: Windows only; on a Mac there are none to close, so Clean up leaves VS Code open there as before.
    public static IReadOnlyList<(nint Handle, string Title)> CodeWindows()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var code = Process.GetProcesses().Where(p => p.ProcessName.StartsWith("Code", StringComparison.OrdinalIgnoreCase))
            .Select(p => (uint)p.Id).ToHashSet();
        var windows = new List<(nint, string)>();
        EnumWindows((window, _) =>
        {
            var title = new StringBuilder(512);
            if (IsWindowVisible(window) && GetWindowThreadProcessId(window, out var process) != 0 && code.Contains(process)
                && GetWindowText(window, title, title.Capacity) > 0)
                windows.Add((window, title.ToString()));
            return true;
        }, 0);
        return windows;
    }

    /// <summary>Closes windows as their close button does, then waits for them to go.</summary>
    /// <returns>False when one is still open after <paramref name="wait"/>, e.g. asking whether to save changes.</returns>
    public static bool CloseWindows(IReadOnlyList<nint> windows, TimeSpan wait)
    {
        foreach (var window in windows) PostMessage(window, WmClose, 0, 0);
        var clock = Stopwatch.StartNew();
        while (windows.Any(IsWindow) && clock.Elapsed < wait) Thread.Sleep(100);
        return !windows.Any(IsWindow);
    }

    static void Start(string[] command)
    {
        var psi = new ProcessStartInfo(command[0]) { CreateNoWindow = true };
        foreach (var arg in command[1..]) psi.ArgumentList.Add(arg);
        Process.Start(psi)?.Dispose();
    }

    const uint WmClose = 0x0010;

    delegate bool EnumWindowsProc(nint window, nint state);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, nint state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint window, StringBuilder title, int capacity);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
}
