using Avalonia.Media;
using Avalonia.Media.Immutable;
using Overworked.Core;

namespace Overworked.App.ViewModels;

/// <summary>
/// The colours rows are drawn in. Each repo with worktrees gets a lane colour of its own, like the lanes in
/// `git log --graph`. Each ticket in view gets a pill colour of its own, shared by every row tagged with it, so one
/// ticket's work across several repos reads as a group. Up to each palette's size, no two share a colour.
/// </summary>
public sealed class RowColours
{
    // Muted, for lines and dots.
    static readonly IBrush[] LanePalette = Solid("#6FA3DE", "#58B8A4", "#B391DC", "#DA8D61", "#93BC6C", "#D27E9F");
    // Light enough for dark text in both themes, hues spread apart, and clear of the PR pill's red.
    static readonly IBrush[] TicketPalette = Solid("#7AA7F0", "#5FC4A8", "#C39BF0", "#F09A6A", "#E585B6", "#A8C76A", "#E8C15A");

    readonly IReadOnlyDictionary<string, int> _lanes, _tickets;

    RowColours(IReadOnlyDictionary<string, int> lanes, IReadOnlyDictionary<string, int> tickets) =>
        (_lanes, _tickets) = (lanes, tickets);

    public static RowColours For(IReadOnlyList<Repo> repos)
    {
        var inFlight = repos.Where(r => r.Linked.Any()).Select(r => r.Name);
        var tickets = repos.SelectMany(r => r.Worktrees).Select(w => w.TicketKey).OfType<string>();
        return new RowColours(LaneSlots(inFlight, LanePalette.Length), TicketSlots(tickets, TicketPalette.Length));
    }

    /// <summary>A repo with no worktrees draws no lane, so it just gets the colour it would prefer.</summary>
    public IBrush LaneFor(Repo repo) =>
        LanePalette[_lanes.TryGetValue(repo.Name, out var slot) ? slot : Hash(repo.Name) % LanePalette.Length];

    public IBrush? TicketColourFor(string? ticketKey) =>
        ticketKey is not null && _tickets.TryGetValue(ticketKey, out var slot) ? TicketPalette[slot] : null;

    /// <summary>Lanes for the repos with worktrees, in list order: in a clash, the repo higher up keeps its colour.</summary>
    internal static Dictionary<string, int> LaneSlots(IEnumerable<string> repoNames, int slotCount) =>
        AssignSlots(repoNames.Order(StringComparer.OrdinalIgnoreCase), slotCount);

    /// <summary>Ticket colours, oldest ticket first: in a clash the older one keeps its colour, so a new ticket never recolours one already there.</summary>
    internal static Dictionary<string, int> TicketSlots(IEnumerable<string> ticketKeys, int slotCount) =>
        AssignSlots(ticketKeys.OrderBy(key => key.Length).ThenBy(key => key, StringComparer.OrdinalIgnoreCase), slotCount); // PROJ-999 before PROJ-1000

    /// <summary>
    /// A slot per key, all different while there are no more keys than slots. Each key prefers the slot it hashes to, so
    /// it keeps its colour as others come and go; when two want one slot, the later key takes the next free one.
    /// </summary>
    static Dictionary<string, int> AssignSlots(IEnumerable<string> keysByPriority, int slotCount)
    {
        var slots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var taken = new bool[slotCount];
        foreach (var key in keysByPriority.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var slot = Hash(key) % slotCount;
            // With every slot taken this goes full circle, and the key shares its preferred colour.
            for (var tried = 0; taken[slot] && tried < slotCount; tried++) slot = (slot + 1) % slotCount;
            taken[slot] = true;
            slots[key] = slot;
        }
        return slots;
    }

    // Not string.GetHashCode: that changes every run, and colours shouldn't.
    internal static int Hash(string key) => key.ToLowerInvariant().Aggregate(17, (hash, c) => hash * 31 + c) & int.MaxValue;

    static IBrush[] Solid(params string[] hexes) =>
        hexes.Select(hex => (IBrush)new ImmutableSolidColorBrush(Color.Parse(hex))).ToArray();
}
