using Overworked.App.ViewModels;
using Overworked.Core;

namespace Overworked.App.Tests;

/// <summary>
/// Removing a worktree asks at once, from the uncommitted changes the last scan showed, and asks again only about work
/// that box didn't mention once the proper check has run.
/// </summary>
public sealed class RemoveWorktreeTests
{
    static readonly Submodule Infra = new("infra", "/r/.worktrees/app/x/infra", Branch: null, PointerMoved: false, ChangedFiles: 0, UnpushedCommits: 1);

    [Theory]
    [InlineData(3, 3, false, true)]  // the box said "3 uncommitted changes": agreed
    [InlineData(3, 5, false, true)]  // more changes since the scan, but the same loss the user agreed to
    [InlineData(0, 1, false, false)] // the box said "deletes its folder": a change since the scan is news
    [InlineData(3, 3, true, false)]  // work inside a submodule is never in the box
    [InlineData(0, 0, true, false)]
    public void Asks_again_only_about_work_the_first_box_did_not_mention(int shown, int changes, bool submoduleWork, bool agreed) =>
        Assert.Equal(agreed, MainViewModel.AlreadyAgreed(shown, new WorkAtRisk(changes, submoduleWork ? [Infra] : [])));
}
