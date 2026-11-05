using Gitunia.Core.Graph;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

public class GraphLayoutShapeTests
{
    [Fact]
    public void GivenNoCommits_WhenLayingOut_ThenThereAreNoRows() =>
        Assert.Empty(GraphLayout.Compute([]));

    [Fact]
    public void GivenLinearHistory_WhenLayingOut_ThenEveryCommitSitsInLaneZeroWithOneLane()
    {
        var rows = GraphLayout.Compute([Commit(1, 2), Commit(2, 3), Commit(3, 4), Commit(4)]);

        foreach (var row in rows)
        {
            Assert.Equal(0, row.Lane);
            Assert.Equal(1, row.LaneCount);
        }

        Assert.Equal([new GraphEdge(GraphEdgeKind.Outgoing, 0, 0)], Shape(rows[0].Edges));
        Assert.Equal(
            [new GraphEdge(GraphEdgeKind.Incoming, 0, 0), new GraphEdge(GraphEdgeKind.Outgoing, 0, 0)],
            Shape(rows[1].Edges));
        Assert.Equal([new GraphEdge(GraphEdgeKind.Incoming, 0, 0)], Shape(rows[3].Edges));
    }

    [Fact]
    public void GivenMergeCommit_WhenLayingOut_ThenSecondParentOpensALaneThatJoinsBack()
    {
        // 1 merges 2 and 3, both children of 4.
        var rows = GraphLayout.Compute([Commit(1, 2, 3), Commit(2, 4), Commit(3, 4), Commit(4)]);

        Assert.Equal(
            [new GraphEdge(GraphEdgeKind.Outgoing, 0, 0), new GraphEdge(GraphEdgeKind.Outgoing, 0, 1)],
            Shape(rows[0].Edges));
        Assert.Equal(2, rows[0].LaneCount);
        Assert.Equal(1, rows[2].Lane);
        Assert.Contains(new GraphEdge(GraphEdgeKind.Outgoing, 1, 1), Shape(rows[2].Edges));
        Assert.Equal(0, rows[3].Lane);
        Assert.Equal(
            [new GraphEdge(GraphEdgeKind.Incoming, 0, 0), new GraphEdge(GraphEdgeKind.Incoming, 1, 0)],
            Shape(rows[3].Edges));
    }

    [Fact]
    public void GivenMergedTipThatAnotherBranchForkedFrom_WhenLayingOut_ThenTheMergeOpensItsOwnLaneThatJoinsAtTheTip()
    {
        // 1 is a newer branch tip on top of 3; 2 merges 4 and 3; the merged branch tip 3 is also the parent of 1.
        var rows = GraphLayout.Compute([Commit(1, 3), Commit(2, 4, 3), Commit(4, 5), Commit(3, 5), Commit(5)]);

        Assert.Equal(1, rows[1].Lane);
        Assert.Equal(3, rows[1].LaneCount);
        Assert.Contains(new GraphEdge(GraphEdgeKind.Outgoing, 1, 2), Shape(rows[1].Edges));
        Assert.Equal(0, rows[3].Lane);
        Assert.Equal(
            [new GraphEdge(GraphEdgeKind.Incoming, 0, 0), new GraphEdge(GraphEdgeKind.Incoming, 2, 0)],
            Shape(rows[3].Edges.Where(edge => edge.Kind == GraphEdgeKind.Incoming).ToArray()));
    }

    [Fact]
    public void GivenBackMergeOfALineOfMergesRightNextToIt_WhenLayingOut_ThenTheMergeEdgeJoinsThatLaneFromTheLeft()
    {
        // The shape of Geeratya 047adcc: 1 merges 2 (main) and 3 (feature); 3 is a back-merge of 2 into the feature.
        var commits = new[] { Commit(1, 2, 3), Commit(3, 4, 2), Commit(4, 5), Commit(2, 5), Commit(5) };
        var rows = GraphLayout.Compute(commits);

        var backMerge = rows[1];
        Assert.Equal(1, backMerge.Lane);
        Assert.Equal(2, backMerge.LaneCount);
        Assert.Contains(new GraphEdge(GraphEdgeKind.Outgoing, 1, 0), Shape(backMerge.Edges));
        Assert.Contains(new GraphEdge(GraphEdgeKind.Through, 0, 0), Shape(backMerge.Edges));
        Assert.Equal(2, rows.Max(row => row.LaneCount));
        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
    }

    [Fact]
    public void GivenBackMergeWithALaneBetweenItAndTheLineOfMerges_WhenLayingOut_ThenTheMergedParentOpensItsOwnLane()
    {
        // Lane 0 continues a line of merges waiting for 2; lane 1 waits for 9; the back-merge 3 (child 10) sits in lane 2.
        var commits = new[]
        {
            Commit(1, 2, 9), Commit(10, 3), Commit(3, 4, 2), Commit(4, 5), Commit(2, 5), Commit(9, 5), Commit(5),
        };
        var rows = GraphLayout.Compute(commits);

        Assert.Equal(2, rows[2].Lane);
        Assert.DoesNotContain(rows[2].Edges, edge => edge.Kind == GraphEdgeKind.Outgoing && edge.ToLane < 2);
        Assert.Contains(new GraphEdge(GraphEdgeKind.Outgoing, 2, 3), Shape(rows[2].Edges));
        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
    }

    [Fact]
    public void GivenMergedBranchAdjacentToAPlainBranchLane_WhenLayingOut_ThenTheMergeStillOpensItsOwnLane()
    {
        // Lane 0 continues the plain commit 1 (not a merge), so it is not a line of merges (#1224 keeps its own lane).
        var rows = GraphLayout.Compute([Commit(1, 3), Commit(2, 4, 3), Commit(4, 5), Commit(3, 5), Commit(5)]);

        Assert.Contains(new GraphEdge(GraphEdgeKind.Outgoing, 1, 2), Shape(rows[1].Edges));
    }

    [Fact]
    public void GivenOctopusMerge_WhenLayingOut_ThenEveryParentGetsItsOwnLane()
    {
        var rows = GraphLayout.Compute([Commit(1, 2, 3, 4, 5), Commit(2, 6), Commit(3, 6), Commit(4, 6), Commit(5, 6), Commit(6)]);

        Assert.Equal(4, rows[0].LaneCount);
        Assert.Equal([0, 1, 2, 3], rows[0].Edges.Select(edge => edge.ToLane));
        Assert.Equal([0, 1, 2, 3], rows.Skip(1).Take(4).Select(row => row.Lane));
        Assert.Equal(0, rows[5].Lane);
        Assert.Equal(4, rows[5].Edges.Count(edge => edge.Kind == GraphEdgeKind.Incoming));
    }

    [Fact]
    public void GivenBranchFanOut_WhenLayingOut_ThenParallelTipsUseSeparateLanesAndConvergeOnTheBase()
    {
        // Tips 1-4 each have a private parent (5-8) and all of those are children of 9.
        var rows = GraphLayout.Compute(
        [
            Commit(1, 5), Commit(2, 6), Commit(3, 7), Commit(4, 8),
            Commit(5, 9), Commit(6, 9), Commit(7, 9), Commit(8, 9), Commit(9),
        ]);

        Assert.Equal([0, 1, 2, 3], rows.Take(4).Select(row => row.Lane));
        Assert.Equal(4, rows.Max(row => row.LaneCount));
        Assert.Equal([0, 1, 2, 3], rows.Skip(4).Take(4).Select(row => row.Lane));
        Assert.Equal(0, rows[8].Lane);
        Assert.Equal(4, rows[8].Edges.Count(edge => edge.Kind == GraphEdgeKind.Incoming));
    }

    [Fact]
    public void GivenManyChildrenOfOneCommit_WhenLayingOut_ThenEachKeepsItsLaneUntilTheyJoinAtTheParent()
    {
        var rows = GraphLayout.Compute([Commit(1, 5), Commit(2, 5), Commit(3, 5), Commit(4, 5), Commit(5)]);

        Assert.Equal([0, 1, 2, 3, 0], rows.Select(row => row.Lane));
        Assert.Equal([0, 1, 2, 3], rows[4].Edges.Select(edge => edge.FromLane));
        Assert.All(rows[4].Edges, edge => Assert.Equal(GraphEdgeKind.Incoming, edge.Kind));
    }

    [Fact]
    public void GivenRootCommits_WhenLayingOut_ThenTheirLaneIsFreedForTheNextCommit()
    {
        var rows = GraphLayout.Compute([Commit(1), Commit(2, 3), Commit(3), Commit(4)]);

        Assert.All(rows, row => Assert.Equal(0, row.Lane));
        Assert.Empty(rows[0].Edges);
        Assert.Equal(1, rows[0].LaneCount);
    }

    [Fact]
    public void GivenFirstParentAlreadyTracked_WhenLayingOut_ThenTheNodeLaneKeepsWaitingAndTheSecondParentOpensANewOne()
    {
        // 1 and 2 both lead to 3; 2 also merges 4, which gets a lane of its own.
        var rows = GraphLayout.Compute([Commit(1, 3), Commit(2, 3, 4), Commit(3), Commit(4)]);

        Assert.Equal(1, rows[1].Lane);
        Assert.Equal(
            [
                new GraphEdge(GraphEdgeKind.Through, 0, 0),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 2),
            ],
            Shape(rows[1].Edges));
        Assert.Equal(2, rows[3].Lane);
    }

    [Fact]
    public void GivenParentOutsideTheStream_WhenLayingOut_ThenItsLaneStaysOpen()
    {
        var builder = new GraphLayoutBuilder();

        builder.Append(Commit(1, 99));

        Assert.Equal(1, builder.OpenLaneCount);
    }

    [Fact]
    public void GivenCommitsAppendedInChunks_WhenReadingRows_ThenAppendReturnsOnlyTheNewRows()
    {
        var builder = new GraphLayoutBuilder();

        var first = builder.AppendRange([Commit(1, 2), Commit(2, 3)]);
        var second = builder.AppendRange([Commit(3)]);

        Assert.Equal(2, first.Count);
        Assert.Single(second);
        Assert.Equal(3, builder.Rows.Count);
    }
}
