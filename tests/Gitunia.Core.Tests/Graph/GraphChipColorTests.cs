using Gitunia.Core.Graph;
using Gitunia.Git;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class GraphChipColorTests
{
    [Fact]
    public void GivenABranchStackedOnAnother_WhenLayingOut_ThenTheChipsDifferAndTheLaneKeepsOneColour()
    {
        // 1 is "test", 2 is "main" (its parent on the same lane), 3 the root.
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3) };
        var tips = new Dictionary<ObjectId, string> { [Id(1)] = "test", [Id(2)] = "main" };

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.All(rows, row => Assert.Equal(0, row.Lane));
        Assert.Single(rows.Select(row => row.NodeColor).Distinct());
        var testChip = Assert.Single(rows[0].LabelSlots!);
        var mainChip = Assert.Single(rows[1].LabelSlots!);
        Assert.Equal("test", testChip.Name);
        Assert.Equal(rows[0].NodeColor, testChip.Slot);
        Assert.Equal("main", mainChip.Name);
        Assert.NotEqual(testChip.Slot, mainChip.Slot);
    }

    [Fact]
    public void GivenAStackedBranchWhoseNameHashesToTheLaneColour_WhenLayingOut_ThenItMovesToAFreeColour()
    {
        var lane = "test";
        var stacked = Enumerable.Range(0, 1000).Select(number => $"stacked-{number}")
            .First(name => BranchColors.PreferredSlot(name) == BranchColors.PreferredSlot(lane));
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3) };
        var tips = new Dictionary<ObjectId, string> { [Id(1)] = lane, [Id(2)] = stacked };

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.NotEqual(rows[0].LabelSlots![0].Slot, rows[1].LabelSlots![0].Slot);
    }

    [Fact]
    public void GivenALocalAndARemoteTipOfOneNameOnOneLane_WhenLayingOut_ThenBothChipsShareTheColour()
    {
        // "main" on 1 and "origin/main" (short name main) further down the same lane.
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3) };
        var tips = new Dictionary<ObjectId, string> { [Id(1)] = "main", [Id(3)] = "main" };

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Equal(rows[0].LabelSlots![0].Slot, rows[2].LabelSlots![0].Slot);
    }

    [Fact]
    public void GivenSeveralBranchesOnOneCommit_WhenLayingOut_ThenEveryNameGetsItsOwnColour()
    {
        var commits = new[] { Commit(1, 2), Commit(2) };
        var tips = new Dictionary<ObjectId, string> { [Id(1)] = "alpha" };
        var extras = new Dictionary<ObjectId, IReadOnlyList<string>> { [Id(1)] = ["beta", "gamma"] };

        var rows = new GraphLayoutBuilder(tips, BranchColors.SlotCount, extras).AppendRange(commits);

        var slots = rows[0].LabelSlots!;
        Assert.Equal(["alpha", "beta", "gamma"], slots.Select(slot => slot.Name));
        Assert.Equal(3, slots.Select(slot => slot.Slot).Distinct().Count());
        Assert.Equal(slots[0].Slot, rows[0].NodeColor);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(12)]
    public void GivenAsManyStackedBranchesAsColours_WhenLayingOut_ThenNoTwoChipsOnScreenShareAColour(int colorCount)
    {
        var commits = Enumerable.Range(1, colorCount).Select(number => Commit(number, number + 1))
            .Append(Commit(colorCount + 1)).ToArray();
        var tips = Enumerable.Range(1, colorCount).ToDictionary(Id, number => $"branch-{number}");

        var rows = new GraphLayoutBuilder(tips, colorCount).AppendRange(commits);

        var slots = rows.Take(colorCount).Select(row => row.LabelSlots![0].Slot).ToArray();
        Assert.Equal(colorCount, slots.Distinct().Count());
    }

    [Fact]
    public void GivenMoreStackedBranchesThanColours_WhenLayingOut_ThenEveryColourIsUsedBeforeOneRepeats()
    {
        const int colorCount = 4;
        var commits = Enumerable.Range(1, 10).Select(number => Commit(number, number + 1))
            .Append(Commit(11)).ToArray();
        var tips = Enumerable.Range(1, 10).ToDictionary(Id, number => $"branch-{number}");

        var rows = new GraphLayoutBuilder(tips, colorCount).AppendRange(commits);

        Assert.Equal(colorCount, rows.Take(colorCount).Select(row => row.LabelSlots![0].Slot).Distinct().Count());
        Assert.All(rows.Take(10), row => Assert.InRange(row.LabelSlots![0].Slot, 0, colorCount - 1));
    }

    [Fact]
    public void GivenTheSameInputTwice_WhenLayingOutBranches_ThenTheChipColoursAreTheSame()
    {
        var commits = RandomHistory(11, count: 200, window: 6);
        var tips = commits.Where((_, index) => index % 3 == 0)
            .ToDictionary(commit => commit.Id, commit => $"branch-{commit.Id.GetHashCode() % 13}");

        var first = new GraphLayoutBuilder(tips).AppendRange(commits);
        var second = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Equal(
            first.Select(row => row.LabelSlots?.Select(slot => slot.Slot).ToArray()),
            second.Select(row => row.LabelSlots?.Select(slot => slot.Slot).ToArray()));
    }

    [Fact]
    public void GivenACommitWithoutBranchNames_WhenLayingOut_ThenItHasNoChipColours()
    {
        var rows = new GraphLayoutBuilder().AppendRange([Commit(1, 2), Commit(2)]);

        Assert.All(rows, row => Assert.Null(row.LabelSlots));
    }
}
