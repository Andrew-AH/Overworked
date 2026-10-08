using Overworked.Core;

namespace Overworked.Core.Tests;

public sealed class TagsTests
{
    [Theory]
    // A ticket's own page, on any company's Jira (cloud or self-hosted).
    [InlineData("https://example.atlassian.net/browse/PROJ-9999", "PROJ-9999")]
    [InlineData("https://example.atlassian.net/browse/PROJ-9999/", "PROJ-9999")]
    [InlineData("https://jira.example.com/browse/ABC2-17", "ABC2-17")]
    // A board or backlog with the ticket open.
    [InlineData("https://example.atlassian.net/jira/software/c/projects/PROJ/boards/178?selectedIssue=PROJ-941", "PROJ-941")]
    [InlineData("https://example.atlassian.net/jira/software/projects/PROJ/boards/3/backlog?quickFilter=12&selectedIssue=PROJ-8", "PROJ-8")]
    // Issue lists, and trackers that put a title after the key.
    [InlineData("https://example.atlassian.net/jira/software/projects/PROJ/issues/PROJ-5", "PROJ-5")]
    [InlineData("https://linear.app/example/issue/ENG-123/fix-the-login-page", "ENG-123")]
    // Links that name no ticket.
    [InlineData("https://example.atlassian.net/jira/software/c/projects/PROJ/boards/178", null)]
    [InlineData("https://example.atlassian.net/jira/your-work", null)]
    [InlineData("PROJ-123", null)]
    public void Finds_the_ticket_a_link_names(string url, string? key) =>
        Assert.Equal(key, Tags.TicketKeyOf(url));

    [Theory]
    [InlineData(TagKind.PullRequest, "https://github.com/example/app/pull/7", true)]
    [InlineData(TagKind.PullRequest, "http://github.com/example/app/pull/7", false)] // https only
    [InlineData(TagKind.PullRequest, "pull/7", false)]
    [InlineData(TagKind.Ticket, "https://example.atlassian.net/browse/PROJ-123", true)]
    [InlineData(TagKind.Ticket, "https://example.atlassian.net/jira/software/c/projects/PROJ/boards/178?selectedIssue=PROJ-941", true)]
    [InlineData(TagKind.Ticket, "https://example.atlassian.net/jira/your-work", false)] // names no ticket
    [InlineData(TagKind.Ticket, "PROJ-123", false)]                                    // a key, but no link
    public void Accepts_only_links_that_make_a_tag(TagKind kind, string url, bool accepted) =>
        Assert.Equal(accepted, Tags.ProblemWith(kind, url) is null);

    [Fact]
    public void A_ticket_shows_its_key_whichever_link_it_was_tagged_with()
    {
        Worktree Tagged(string url) => new("/r/app", "proj-941", IsMain: false, IsMissing: false, TicketUrl: url);

        Assert.Equal("PROJ-941", Tagged("https://example.atlassian.net/browse/PROJ-941").TicketKey);
        Assert.Equal("PROJ-941", Tagged("https://example.atlassian.net/jira/software/c/projects/PROJ/boards/178?selectedIssue=PROJ-941").TicketKey);
        Assert.Equal("Ticket", Tagged("https://example.com/hand-edited").TicketKey); // still shows, still opens
    }
}
