using Overworked.Core;

namespace Overworked.Core.Tests;

public sealed class WorkAtRiskTests
{
    [Fact]
    public void Describes_each_kind_of_work_that_would_be_lost()
    {
        Assert.Equal("1 uncommitted change", new WorkAtRisk(1, []).Describe());
        Assert.Equal("1 unpushed commit in infra", new WorkAtRisk(0, [Unpushed("infra", 1)]).Describe());
        Assert.Equal("3 uncommitted changes and 1 unpushed commit in infra and 2 unpushed commits in db",
            new WorkAtRisk(3, [Unpushed("infra", 1), Unpushed("db", 2)]).Describe());
        Assert.Equal("2 uncommitted changes in infra and 1 unpushed commit in infra",
            new WorkAtRisk(0, [Unpushed("infra", 1) with { ChangedFiles = 2 }]).Describe());
    }

    static Submodule Unpushed(string name, int commits) =>
        new(name, name, Branch: null, PointerMoved: false, ChangedFiles: 0, UnpushedCommits: commits);
}
