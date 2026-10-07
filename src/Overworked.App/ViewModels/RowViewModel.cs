using Avalonia.Media;
using Overworked.Core;

namespace Overworked.App.ViewModels;

/// <summary>A row in the list: a repo (<see cref="RepoRow"/>) or one of its linked worktrees (<see cref="WorktreeRow"/>).</summary>
public abstract class RowViewModel(Repo repo, RowColours colours) : ObservableObject
{
    public Repo Repo { get; } = repo;

    /// <summary>The repo's colour, shared by its worktrees.</summary>
    public IBrush Lane { get; } = colours.LaneFor(repo);

    /// <summary>The checkout this row shows; null for a repo git couldn't read.</summary>
    public abstract Worktree? Worktree { get; }

    public abstract string Path { get; }

    /// <summary>What the row is called in dialogs and messages.</summary>
    public abstract string Title { get; }

    public string? Branch => Worktree?.Branch;
    public bool HasBranch => Branch is not null;
    public bool IsMissing => Worktree?.IsMissing == true;
    public virtual bool IsBroken => IsMissing;

    /// <summary>The row's second line: its branch, or what's wrong.</summary>
    public virtual string Detail => IsMissing ? "folder is gone — remove to clean up" : Branch ?? "detached HEAD";

    public bool IsDirty => Worktree?.ChangedFiles > 0;
    public string ChangesText => $"{Worktree?.ChangedFiles} changed";

    public string? TicketUrl => Worktree?.TicketUrl;
    public string? TicketKey => Worktree?.TicketKey;
    public bool HasTicket => TicketKey is not null;
    public IBrush? TicketColour => colours.TicketColourFor(TicketKey);

    public string? PullRequestUrl => Worktree?.PullRequestUrl;
    public bool HasPullRequest => PullRequestUrl is not null;
    // GitHub links end in the PR number.
    public string PullRequestText => int.TryParse(PullRequestUrl?.Split('/')[^1], out var number) ? $"PR #{number}" : "PR";
}
