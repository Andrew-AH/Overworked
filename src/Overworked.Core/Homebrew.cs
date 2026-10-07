namespace Overworked.Core;

public static class Homebrew
{
    /// <summary>
    /// On macOS, puts Homebrew's folders on this process's PATH, so git is found. Programs started from Finder, the Dock
    /// or another app get a bare PATH without them. Call once at startup, before running git.
    /// </summary>
    public static void AddToPath()
    {
        if (OperatingSystem.IsMacOS())
            Environment.SetEnvironmentVariable("PATH", "/opt/homebrew/bin:/usr/local/bin:" + Environment.GetEnvironmentVariable("PATH"));
    }
}
