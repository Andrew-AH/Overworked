using System.Text.RegularExpressions;
using System.Web;

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
    /// <exception cref="OverworkedException">The link won't do (<see cref="ProblemWith"/>).</exception>
    public static void Set(string repoPath, string branch, TagKind kind, string url)
    {
        if (ProblemWith(kind, url) is { } problem) throw new OverworkedException(problem);
        Git.Run(repoPath, "config", "--local", $"branch.{branch}.{Setting(kind)}", url);
    }

    /// <summary>Why <paramref name="url"/> can't be this kind of tag, or null when it can.</summary>
    public static string? ProblemWith(TagKind kind, string url)
    {
        var isLink = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        if (kind == TagKind.PullRequest && !isLink)
            return $"Not a pull request link: {url}";
        if (kind == TagKind.Ticket && !(isLink && TicketKeyOf(url) is not null))
            return $"Not a ticket link: {url}. It should name the ticket, like https://.../browse/PROJ-123 or a board link with ?selectedIssue=PROJ-123.";
        return null;
    }

    /// <summary>
    /// The ticket a link names, from any tracker that uses keys like PROJ-123: a board or backlog with it open
    /// (…/boards/178?selectedIssue=PROJ-123), or else the last part of the path that's a key (…/browse/PROJ-123,
    /// …/issues/PROJ-123, …/issue/PROJ-123/its-title). Null when the link names none.
    /// </summary>
    public static string? TicketKeyOf(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (HttpUtility.ParseQueryString(uri.Query)["selectedIssue"] is { } selected && IsTicketKey(selected)) return selected;
        return uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(IsTicketKey);
    }

    /// <summary>
    /// The link a branch's ticket would have, going by its name (proj-123-retry-payments names PROJ-123, as does
    /// feature/PROJ-123) and a ticket of the same project already tagged somewhere, whose link shows where the tracker is
    /// and how it links a ticket. Null when the name starts with no key, or no tagged ticket shares its project: a
    /// branch like fix-123-login must not become FIX-123.
    /// </summary>
    public static string? SuggestTicketUrl(string branch, IEnumerable<string> knownTicketUrls)
    {
        var named = Regex.Match(branch, @"^(?:[^/]+/)?(?<project>[A-Za-z][A-Za-z0-9_]*)-(?<number>[0-9]+)(?=$|[-_/.])");
        if (!named.Success) return null;
        foreach (var known in knownTicketUrls)
        {
            if (TicketKeyOf(known) is not { } knownKey || !Uri.TryCreate(known, UriKind.Absolute, out var uri)) continue;
            var project = knownKey[..knownKey.LastIndexOf('-')];
            if (!project.Equals(named.Groups["project"].Value, StringComparison.OrdinalIgnoreCase)) continue;
            var key = $"{project}-{named.Groups["number"].Value}";
            // The known link's path up to its key, then ours: /browse/PROJ-9 → /browse/PROJ-123, /issue/ENG-1/title →
            // /issue/ENG-55. A board link names its ticket in the query; a ticket's own page on such a site is /browse/<key>.
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var at = Array.FindIndex(segments, s => s.Equals(knownKey, StringComparison.OrdinalIgnoreCase));
            string[] path = at < 0 ? ["browse"] : segments[..at];
            return $"{uri.Scheme}://{uri.Authority}/{string.Join('/', path.Append(key))}";
        }
        return null;
    }

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

    // A Jira-style key: the project's letters (and digits), a dash, the number.
    static bool IsTicketKey(string text) => Regex.IsMatch(text, "^[A-Za-z][A-Za-z0-9_]*-[0-9]+$");
}

/// <summary>A branch's tags.</summary>
internal readonly record struct BranchTags(string? PullRequestUrl, string? TicketUrl);
