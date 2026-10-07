using System.Diagnostics;
using System.Text;

namespace Overworked.Core;

/// <summary>Runs git. git is the source of truth: Overworked keeps no state of its own about repos.</summary>
static class Git
{
    /// <summary>Runs git in <paramref name="workingDirectory"/> and returns what it printed.</summary>
    /// <exception cref="OverworkedException">git failed; the message is its error output.</exception>
    public static string Run(string workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        // Worktrees under <root>/.worktrees/<repo>/<branch> push deep files past Windows' 260-char limit.
        // Passed via -c it also reaches the git processes this one spawns (e.g. submodule checkouts).
        if (OperatingSystem.IsWindows()) { psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("core.longpaths=true"); }
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        // Never block on a credential prompt; fail instead (matters when run in the background).
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = Process.Start(psi)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new OverworkedException(stderr.Result.Trim() is { Length: > 0 } error ? error : $"git {string.Join(' ', args)} failed");
        return stdout;
    }

    /// <summary>Runs git for a yes/no answer: whether it succeeded.</summary>
    public static bool Succeeds(string workingDirectory, params string[] args)
    {
        try { Run(workingDirectory, args); return true; }
        catch (OverworkedException) { return false; }
    }

    /// <summary>The non-empty lines of git's output, whichever line endings it has.</summary>
    public static string[] Lines(string output) =>
        output.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
