using Overworked.Core;

namespace Overworked.Core.Tests;

public sealed class TagsTests
{
    [Theory]
    [InlineData(TagKind.PullRequest, "https://github.com/example/app/pull/7", true)]
    [InlineData(TagKind.PullRequest, "http://github.com/example/app/pull/7", false)] // https only
    [InlineData(TagKind.PullRequest, "pull/7", false)]
    [InlineData(TagKind.Ticket, "https://example.atlassian.net/browse/PROJ-123", true)]
    [InlineData(TagKind.Ticket, "https://example.atlassian.net/browse/PROJ-123/", true)]
    [InlineData(TagKind.Ticket, "https://example.atlassian.net/jira/your-work", false)] // doesn't end in a key
    [InlineData(TagKind.Ticket, "PROJ-123", false)]                                    // a key, but no link
    public void Accepts_only_links_that_make_a_tag(TagKind kind, string url, bool accepted) =>
        Assert.Equal(accepted, Tags.ProblemWith(kind, url) is null);
}
