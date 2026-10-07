using System.Text.RegularExpressions;

namespace Overworked.Core;

/// <summary>What a branch can be tagged with: a link to its pull request, or to its ticket.</summary>
public enum TagKind { PullRequest, Ticket }

/// <summary>
/// Links tagged onto branches. They're kept in git config as branch.&lt;name&gt;.overworked-pr and
/// branch.&lt;name&gt;.overworked-ticket, so every worktree sees them and deleting a branch drops its tags.
/// </summary>
public static class Tags
{
    /// <summary>Tags <paramref name="branch"/> with a link, replacing any it had of that kind.</summary>
    /// <exception cref="OverworkedException">It isn't an https link, or a ticket link doesn't end in the ticket's key.</exception>
    public static void Set(string repoPath, string branch, TagKind kind, string url)
    {
        var isLink = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        if (kind == TagKind.PullRequest && !isLink)
            throw new OverworkedException($"Not a pull request link: {url}");
        if (kind == TagKind.Ticket && !(isLink && IsTicketKey(LastSegment(uri!))))
            throw new OverworkedException($"Not a ticket link: {url}. It should end in the ticket's key, like https://.../browse/PROJ-123.");
        Git.Run(repoPath, "config", "--local", $"branch.{branch}.{Setting(kind)}", url);
    }

    /// <summary>A ticket's key, from the end of its link: https://…/browse/PROJ-123 is PROJ-123.</summary>
    public static string TicketKeyOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? LastSegment(uri) : url;

    /// <summary>Each tagged branch's links, by branch name.</summary>
    internal static IReadOnlyDictionary<string, BranchTags> ByBranch(string repoPath)
    {
        string config;
        try { config = Git.Run(repoPath, "config", "--local", "--get-regexp", @"^branch\..*\.overworked-(pr|ticket)$"); }
        catch (OverworkedException) { return new Dictionary<string, BranchTags>(); } // git exits 1 when no branch has one
        return Parse(config);
    }

    /// <summary>`git config --get-regexp` lines: "branch.&lt;name&gt;.overworked-pr &lt;url&gt;". Branch names can't contain spaces.</summary>
    internal static Dictionary<string, BranchTags> Parse(string config)
    {
        var tags = new Dictionary<string, BranchTags>();
        foreach (var line in Git.Lines(config))
        {
            var fields = line.Split(' ', 2);
            var (key, url) = (fields[0], fields.Length > 1 ? fields[1].Trim() : "");
            foreach (var kind in Enum.GetValues<TagKind>())
            {
                var suffix = "." + Setting(kind);
                if (!key.EndsWith(suffix)) continue;
                var branch = key["branch.".Length..^suffix.Length];
                var current = tags.GetValueOrDefault(branch);
                tags[branch] = kind == TagKind.PullRequest ? current with { PullRequestUrl = url } : current with { TicketUrl = url };
            }
        }
        return tags;
    }

    static string Setting(TagKind kind) => kind == TagKind.PullRequest ? "overworked-pr" : "overworked-ticket";

    static string LastSegment(Uri uri) => uri.AbsolutePath.TrimEnd('/').Split('/')[^1];

    // A Jira-style key: project letters, a dash, the number.
    static bool IsTicketKey(string text) => Regex.IsMatch(text, "^[A-Za-z][A-Za-z0-9_]*-[0-9]+$");
}

/// <summary>A branch's tags.</summary>
internal readonly record struct BranchTags(string? PullRequestUrl, string? TicketUrl);
