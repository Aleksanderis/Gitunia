using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

public class WorkingTreePositionTests
{
    [Fact]
    public void GivenNoStatusYet_WhenAskingForThePosition_ThenItIsZeroOfZero()
    {
        Assert.Equal(new WorkingTreePosition(0, 0), WorkingTreePosition.From(null));
    }

    [Fact]
    public void GivenACleanTree_WhenAskingForThePosition_ThenItIsZeroOfZero()
    {
        Assert.Equal(new WorkingTreePosition(0, 0), WorkingTreePosition.From(Tree()));
    }

    [Fact]
    public void GivenTheCursorInStaged_WhenAskingForThePosition_ThenItCountsTheStagedList()
    {
        var tree = Tree(Modified("a"), Modified("b"), StagedModified("s1"), StagedModified("s2"), StagedModified("s3"));
        tree = tree with { Selection = new WorkingTreeSelection(WorkingTreeSection.Staged, "s2", 1) };

        Assert.Equal(new WorkingTreePosition(2, 3), WorkingTreePosition.From(tree));
    }
}
