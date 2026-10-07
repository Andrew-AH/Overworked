namespace Overworked.Core;

/// <summary>The submodules inside a worktree.</summary>
static class Submodules
{
    /// <summary>Initialised submodules. Only read when removing a worktree, to guard their unpushed commits.</summary>
    public static IReadOnlyList<Submodule> List(string worktreePath)
    {
        if (!HasAny(worktreePath)) return [];

        // Not `git submodule status`: it runs `git describe` per submodule, which takes seconds on a big repo.
        return ParseGitlinks(Git.Run(worktreePath, "ls-files", "--stage"))
            // A submodule with no .git inside isn't initialised, so there's nothing on disk to report.
            .Where(link => Path.Exists(Path.Combine(worktreePath, link.Path, ".git")))
            .Select(link => Read(worktreePath, link))
            .ToList();
    }

    /// <summary>Checks out a new worktree's submodules, all the way down.</summary>
    public static void Init(string worktreePath)
    {
        if (HasAny(worktreePath)) Git.Run(worktreePath, "submodule", "update", "--init", "--recursive");
    }

    static bool HasAny(string worktreePath) => File.Exists(Path.Combine(worktreePath, ".gitmodules"));

    static Submodule Read(string worktreePath, Gitlink link)
    {
        var path = Path.GetFullPath(Path.Combine(worktreePath, link.Path));
        var status = ParseStatus(Git.Run(path, "status", "--porcelain=v2", "--branch"));
        var unpushed = int.Parse(Git.Run(path, "rev-list", "--count", "HEAD", "--not", "--remotes").Trim());
        return new Submodule(link.Path, path, status.Branch, PointerMoved: status.Commit != link.Commit,
            status.ChangedFiles, unpushed);
    }

    /// <summary>A submodule as its parent repo records it: where it goes and which commit it should be on.</summary>
    internal readonly record struct Gitlink(string Path, string Commit);

    internal readonly record struct Status(string? Branch, string? Commit, int ChangedFiles);

    /// <summary>
    /// Submodule lines of `git ls-files --stage`: "160000 &lt;commit&gt; &lt;stage&gt;\t&lt;path&gt;".
    /// Stage 0 only; the others are merge conflicts.
    /// </summary>
    internal static IEnumerable<Gitlink> ParseGitlinks(string lsFilesStage) =>
        Git.Lines(lsFilesStage)
            .Where(line => line.StartsWith("160000 "))
            .Select(line => line.Split('\t', 2))
            .Select(parts => (Fields: parts[0].Split(' '), Path: parts[1]))
            .Where(entry => entry.Fields[2] == "0")
            .Select(entry => new Gitlink(entry.Path, Commit: entry.Fields[1]));

    /// <summary>`git status --porcelain=v2 --branch`: "# branch.*" headers, then one line per changed path.</summary>
    internal static Status ParseStatus(string porcelain)
    {
        var lines = Git.Lines(porcelain);
        string? Header(string prefix) => lines.FirstOrDefault(l => l.StartsWith(prefix))?[prefix.Length..];

        var head = Header("# branch.head ");
        return new Status(
            Branch: head is null or "(detached)" ? null : head,
            Commit: Header("# branch.oid "),
            ChangedFiles: lines.Count(l => !l.StartsWith('#')));
    }
}
