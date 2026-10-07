using Overworked.App.ViewModels;
using Overworked.Core;

namespace Overworked.App.Tests;

public sealed class RowColoursTests
{
    [Fact]
    public void Tickets_in_flight_each_get_their_own_colour()
    {
        var slots = RowColours.TicketSlots(["PROJ-123", "PROJ-130", "PROJ-145", "PROJ-98"], slotCount: 7);

        Assert.Equal(4, slots.Values.Distinct().Count());
    }

    [Fact]
    public void Tickets_whose_keys_clash_still_get_different_colours()
    {
        Assert.Equal(RowColours.Hash("PROJ-123") % 7, RowColours.Hash("PROJ-130") % 7); // both want one slot

        var slots = RowColours.TicketSlots(["PROJ-123", "PROJ-130"], slotCount: 7);

        Assert.NotEqual(slots["PROJ-123"], slots["PROJ-130"]);
    }

    [Fact]
    public void Only_more_tickets_than_colours_share_one()
    {
        var eight = Enumerable.Range(1, 8).Select(i => $"PROJ-{i}").ToList();

        var slots = RowColours.TicketSlots(eight, slotCount: 7);

        Assert.Equal((8, 7), (slots.Count, slots.Values.Distinct().Count()));
    }

    [Fact]
    public void A_ticket_has_one_colour_wherever_it_is_tagged()
    {
        // The same ticket tagged in several repos, in any case, in any order.
        var one = RowColours.TicketSlots(["PROJ-999", "PROJ-1000", "proj-999", "PROJ-999"], slotCount: 7);
        var other = RowColours.TicketSlots(["PROJ-1000", "PROJ-999"], slotCount: 7);

        Assert.Equal(2, one.Count);
        Assert.Equal(one["PROJ-999"], other["proj-999"]);
        Assert.Equal(one["PROJ-1000"], other["PROJ-1000"]);
    }

    [Fact]
    public void A_newer_ticket_never_recolours_the_ones_already_there()
    {
        var before = RowColours.TicketSlots(["PROJ-998", "PROJ-999"], slotCount: 7);
        var after = RowColours.TicketSlots(["PROJ-998", "PROJ-999", "PROJ-1000", "PROJ-1001"], slotCount: 7);

        Assert.Equal(before["PROJ-998"], after["PROJ-998"]);
        Assert.Equal(before["PROJ-999"], after["PROJ-999"]);
    }

    [Fact]
    public void Repos_whose_names_clash_still_get_different_lanes()
    {
        Assert.Equal(RowColours.Hash("infra") % 6, RowColours.Hash("web") % 6); // both want one lane

        var slots = RowColours.LaneSlots(["web", "infra"], slotCount: 6);

        Assert.Equal(RowColours.Hash("infra") % 6, slots["infra"]); // higher in the list, so it keeps its lane
        Assert.NotEqual(slots["infra"], slots["web"]);
    }

    [Fact]
    public void Only_repos_with_worktrees_take_up_a_lane()
    {
        var colours = RowColours.For([Repo("infra", worktrees: 0), Repo("web", worktrees: 1)]);

        // infra draws no lane, so web needn't give way: it keeps the colour it hashes to, which infra would have taken.
        Assert.Same(colours.LaneFor(Repo("infra", worktrees: 0)), colours.LaneFor(Repo("web", worktrees: 1)));
    }

    [Fact]
    public void Looks_ticket_colours_up_by_key_in_any_case()
    {
        var colours = RowColours.For([Repo("api", worktrees: 1, ticket: "https://example.atlassian.net/browse/PROJ-1")]);

        Assert.NotNull(colours.TicketColourFor("PROJ-1"));
        Assert.Same(colours.TicketColourFor("PROJ-1"), colours.TicketColourFor("proj-1"));
        Assert.Null(colours.TicketColourFor(null));
    }

    static Repo Repo(string name, int worktrees, string? ticket = null) =>
        new(name, $"/r/{name}",
        [
            new Worktree($"/r/{name}", "main", IsMain: true, IsMissing: false),
            .. Enumerable.Range(1, worktrees).Select(i =>
                new Worktree($"/r/.worktrees/{name}/w{i}", $"w{i}", IsMain: false, IsMissing: false, TicketUrl: ticket)),
        ]);
}
