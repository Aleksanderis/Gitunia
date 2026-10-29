using Gitunia.Core.Graph;
using Gitunia.Git;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class GraphBranchColorTests
{
    [Fact]
    public void GivenABranchTip_WhenLayingOut_ThenItsLaneHasTheSlotOfItsNameAllTheWayDown()
    {
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3) };
        var builder = new GraphLayoutBuilder(new Dictionary<ObjectId, string> { [Id(1)] = "feature" });
        var expected = BranchColors.PreferredSlot("feature");

        var rows = builder.AppendRange(commits);

        Assert.All(rows, row => Assert.Equal(expected, row.NodeColor));
        Assert.All(rows.SelectMany(row => row.Edges), edge => Assert.Equal(expected, edge.Color));
    }

    [Fact]
    public void GivenTwoBranchesOnDifferentCommits_WhenLayingOut_ThenEachLaneHasItsOwnSlot()
    {
        var commits = new[] { Commit(1, 3), Commit(2, 4), Commit(3, 5), Commit(4, 5), Commit(5) };
        var tips = new Dictionary<ObjectId, string> { [Id(1)] = "alpha", [Id(2)] = "beta" };

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.NotEqual(rows[0].NodeColor, rows[1].NodeColor);
        Assert.Equal(rows[0].NodeColor, rows[2].NodeColor);
        Assert.Equal(rows[1].NodeColor, rows[3].NodeColor);
    }

    [Fact]
    public void GivenTwoNamesWithTheSamePreferredSlot_WhenBothLanesAreOpen_ThenTheSecondMovesToAFreeSlot()
    {
        var first = "branch-0";
        var second = Enumerable.Range(1, 1000).Select(number => $"branch-{number}")
            .First(name => BranchColors.PreferredSlot(name) == BranchColors.PreferredSlot(first));
        var commits = new[] { Commit(1, 3), Commit(2, 3), Commit(3) };
        var tips = new Dictionary<ObjectId, string> { [Id(1)] = first, [Id(2)] = second };

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.NotEqual(rows[0].NodeColor, rows[1].NodeColor);
    }

    [Fact]
    public void GivenALocalAndARemoteTipOfTheSameName_WhenLayingOut_ThenBothLanesShareTheSlot()
    {
        // 1 is origin/topic, 2 is topic (both named "topic"), on different commits.
        var commits = new[] { Commit(1, 3), Commit(2, 4), Commit(3, 5), Commit(4, 5), Commit(5) };
        var tips = new Dictionary<ObjectId, string> { [Id(1)] = "topic", [Id(2)] = "topic" };

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Equal(rows[0].NodeColor, rows[1].NodeColor);
    }

    [Fact]
    public void GivenAMergedBranchTip_WhenLayingOut_ThenTheLaneOfTheSecondParentHasThatBranchSlotFromTheMergeOn()
    {
        // 1 merges 2 (main line) and 3, the tip of "feature".
        var commits = new[] { Commit(1, 2, 3), Commit(2, 4), Commit(3, 4), Commit(4) };
        var tips = new Dictionary<ObjectId, string> { [Id(1)] = "main", [Id(3)] = "feature" };

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        var featureSlot = BranchColors.PreferredSlot("feature");
        var mergeLine = rows[0].Edges.Single(edge => edge.Kind == GraphEdgeKind.Outgoing && edge.ToLane == 1);
        Assert.Equal(featureSlot, mergeLine.Color);
        Assert.Equal(featureSlot, rows[2].NodeColor);
        Assert.Equal(BranchColors.PreferredSlot("main"), rows[0].NodeColor);
    }

    [Fact]
    public void GivenNoBranchNames_WhenLayingOut_ThenALaneIsColouredByItsFirstCommitAndStaysSo()
    {
        var commits = new[] { Commit(1, 2), Commit(2) };

        var rows = new GraphLayoutBuilder().AppendRange(commits);

        Assert.Equal(rows[0].NodeColor, rows[1].NodeColor);
        Assert.InRange(rows[0].NodeColor, 0, BranchColors.SlotCount - 1);
    }

    [Fact]
    public void GivenTheSameBranchWithADifferentName_WhenLayingOut_ThenTheLaneFingerprintDiffers()
    {
        var commits = new[] { Commit(1, 2), Commit(2) };
        var otherName = Enumerable.Range(1, 1000).Select(number => $"beta-{number}")
            .First(name => BranchColors.PreferredSlot(name) != BranchColors.PreferredSlot("alpha"));
        var named = new GraphLayoutBuilder(new Dictionary<ObjectId, string> { [Id(1)] = "alpha" });
        var other = new GraphLayoutBuilder(new Dictionary<ObjectId, string> { [Id(1)] = otherName });

        var fingerprintA = named.Append(commits[0]).LaneFingerprint;
        var fingerprintB = other.Append(commits[0]).LaneFingerprint;

        Assert.NotEqual(fingerprintA, fingerprintB);
    }

    [Fact]
    public void GivenAnIncrementalFeed_WhenColouring_ThenRowsEqualTheFullLayout()
    {
        var commits = RandomHistory(7, count: 300, window: 10);
        var tips = commits.Where(commit => commit.Id.GetHashCode() % 5 == 0)
            .ToDictionary(commit => commit.Id, commit => $"branch-{commit.Id.GetHashCode() % 17}");
        var full = new GraphLayoutBuilder(tips).AppendRange(commits);
        var builder = new GraphLayoutBuilder(tips);

        var position = 0;
        while (position < commits.Count)
        {
            builder.AppendRange(commits.Skip(position).Take(37));
            position += 37;
        }

        Assert.True(GraphRowComparer.AreEqual(full, builder.Rows));
    }
}
