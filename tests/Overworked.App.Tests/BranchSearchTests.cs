using Overworked.App.ViewModels;
using Overworked.Core;

namespace Overworked.App.Tests;

public sealed class BranchSearchTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    static readonly string MainFolder = Path.GetFullPath("/r/app");

    [Theory]
    [InlineData("PROJ-123", "", 0)]
    [InlineData("PROJ-123", "proj", 0)]   // prefix, any case
    [InlineData("feature/PROJ-123", "proj", 1)] // substring
    [InlineData("feature/PROJ-123", "fo1", 2)] // letters in order
    [InlineData("PROJ-123", "321", null)]
    public void Scores_prefix_over_substring_over_letters_in_order(string name, string query, int? score) =>
        Assert.Equal(score, BranchSearch.Score(name, query));

    [Fact]
    public void Lists_pickable_branches_first_and_best_matches_first_keeping_newest_first_otherwise()
    {
        var branches = new[]
        {
            Branch("fix/proj-9"),                                         // substring
            Branch("proj-7", checkedOutAt: Path.GetFullPath("/r/.worktrees/app/proj-7")), // prefix, but busy
            Branch("proj-8"),                                             // prefix
            Branch("proj-6"),                                             // prefix, older
        };

        var options = BranchSearch.Filter(branches, "proj", exclude: null, MainFolder, Now);

        Assert.Equal(new[] { "proj-8", "proj-6", "fix/proj-9", "proj-7", "proj" }, options.Select(o => o.Name));
        Assert.Equal(("worktree", false), (options[3].Tag, options[3].CanPick));
    }

    [Fact]
    public void Offers_to_create_a_typed_name_only_when_no_branch_has_it()
    {
        var branches = new[] { Branch("PROJ-1") };

        var create = BranchSearch.Filter(branches, "PROJ-2", exclude: null, MainFolder, Now).Last();
        var none = BranchSearch.Filter(branches, "proj-1", exclude: null, MainFolder, Now);

        Assert.Equal(("+ Create PROJ-2", new BranchChoice("PROJ-2", Create: true)), (create.Label, create.Choice));
        Assert.DoesNotContain(none, o => o.IsNew);
        Assert.DoesNotContain(BranchSearch.Filter(branches, "", exclude: null, MainFolder, Now), o => o.IsNew);
    }

    [Fact]
    public void Leaves_out_the_excluded_branch_and_marks_the_main_folders_branch()
    {
        var branches = new[] { Branch("main", checkedOutAt: MainFolder), Branch("PROJ-1"), Branch("PROJ-2", isRemote: true) };

        var switching = BranchSearch.Filter(branches, "", exclude: "main", MainFolder, Now);
        var opening = BranchSearch.Filter(branches, "", exclude: null, MainFolder, Now);

        Assert.Equal(new[] { "PROJ-1", "PROJ-2" }, switching.Select(o => o.Name));
        Assert.Equal(new[] { ("PROJ-1", "3h"), ("PROJ-2", "origin"), ("main", "main folder") },
            opening.Select(o => (o.Name, o.Tag)));
    }

    [Theory]
    [InlineData(0, "1m")]
    [InlineData(59, "59m")]
    [InlineData(60, "1h")]
    [InlineData(60 * 24 * 3, "3d")]
    public void Shows_ages_compactly(int minutesAgo, string age) =>
        Assert.Equal(age, BranchSearch.Age(Now.AddMinutes(-minutesAgo), Now));

    static BranchRef Branch(string name, string? checkedOutAt = null, bool isRemote = false) =>
        new(name, isRemote, checkedOutAt, LastCommit: Now.AddHours(-3));
}
