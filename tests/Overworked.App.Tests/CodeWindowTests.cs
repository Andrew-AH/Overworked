using Overworked.App.ViewModels;
using Overworked.Core;

namespace Overworked.App.Tests;

/// <summary>Which VS Code windows Clean up closes, from their titles.</summary>
public sealed class CodeWindowTests
{
    static readonly Worktree Opal = new("/r/.worktrees/billing/proj-11", "proj-11", IsMain: false, IsMissing: false);
    static readonly Worktree Feature = new("/r/.worktrees/billing/feature-x", "feature/x", IsMain: false, IsMissing: false);

    [Theory]
    // window.title set to "${activeRepositoryName}${separator}${activeRepositoryBranchName}": repo and branch.
    [InlineData("billing - proj-11", false, true)]
    [InlineData("api - proj-11", false, false)]          // the same branch in another repo
    [InlineData("api - proj-11", true, true)]            // ...but no other checkout has that folder, so it can only be this one
    // VS Code's default title: the open file, the folder, the app; "●" when a file has unsaved changes.
    [InlineData("Program.cs - proj-11 - Visual Studio Code", true, true)]
    [InlineData("● Program.cs - proj-11 - Visual Studio Code", true, true)]
    [InlineData("Program.cs - proj-11 - Visual Studio Code", false, false)] // another repo has a proj-11 folder too
    [InlineData("billing - main", true, false)]
    [InlineData("proj-110 - Visual Studio Code", true, false)]
    public void Closes_only_windows_showing_this_worktree(string title, bool folderNameUnique, bool closes) =>
        Assert.Equal(closes, MainViewModel.ShowsCheckout(title, "billing", Opal, folderNameUnique));

    [Fact]
    public void Knows_a_branch_with_a_slash_by_its_name_or_its_folder()
    {
        Assert.True(MainViewModel.ShowsCheckout("billing - feature/x", "billing", Feature, folderNameUnique: false));
        Assert.True(MainViewModel.ShowsCheckout("Program.cs - feature-x - Visual Studio Code", "billing", Feature, folderNameUnique: true));
    }
}
