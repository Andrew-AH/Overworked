using Overworked.App.ViewModels;
using Overworked.Core;

namespace Overworked.App.Tests;

public sealed class RepoOrderTests
{
    [Fact]
    public void Repos_with_worktrees_come_first_each_group_in_name_order()
    {
        // As a scan returns them: by name.
        var repos = new[] { Repo("design-system", 0), Repo("infra", 0), Repo("mobile-app", 1), Repo("payments-api", 2), Repo("web", 0) };

        var shown = MainViewModel.InFlightFirst(repos).Select(r => r.Name);

        Assert.Equal(new[] { "mobile-app", "payments-api", "design-system", "infra", "web" }, shown);
    }

    static Repo Repo(string name, int worktrees) =>
        new(name, $"/r/{name}",
        [
            new Worktree($"/r/{name}", "main", IsMain: true, IsMissing: false),
            .. Enumerable.Range(1, worktrees).Select(i => new Worktree($"/r/.worktrees/{name}/w{i}", $"w{i}", IsMain: false, IsMissing: false)),
        ]);
}
