using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class GraphBackMergeTests
{
    // A feature branch back-merging main (#1234): main waits in lane 0 for commit 2, another branch holds lane 1, a
    // feature tip (3) sits in lane 2 and merges 2 back. Commit 1 is the PR merge that opened the feature lane.
    public static readonly IReadOnlyList<CommitInfo> BackMerge =
    [
        Commit(20, 1),
        Commit(21, 22),
        Commit(1, 2, 3),
        Commit(3, 4, 2),
        Commit(4, 5),
        Commit(2, 5),
        Commit(22, 5),
        Commit(5),
    ];

    [Fact]
    public void GivenALeftLaneWaitsForTheMergedInParent_WhenLayingOut_ThenTheEdgeJoinsThatLaneAndNoLaneIsAdded()
    {
        var rows = GraphLayout.Compute(BackMerge);

        var merge = rows[3];
        Assert.Equal(2, merge.Lane);
        Assert.Contains(new GraphEdge(GraphEdgeKind.Outgoing, 2, 0), Shape(merge.Edges));
        Assert.Equal(3, merge.LaneCount);
        Assert.All(rows.Skip(3), row => Assert.True(row.LaneCount <= 3));
    }

    [Fact]
    public void GivenSeveralLanesWaitForTheMergedInParent_WhenLayingOut_ThenTheLeftmostOneIsJoined()
    {
        var rows = GraphLayout.Compute(
            [Commit(10, 4), Commit(11, 4), Commit(1, 6, 4), Commit(6, 4), Commit(4)]);

        Assert.Equal(2, rows[2].Lane);
        Assert.Contains(new GraphEdge(GraphEdgeKind.Outgoing, 2, 0), Shape(rows[2].Edges));
        Assert.DoesNotContain(rows[2].Edges, edge => edge.ToLane > 2);
    }

    [Fact]
    public void GivenOctopusMergeWithOneParentAlreadyWaitedFor_WhenLayingOut_ThenOnlyTheOthersOpenLanes()
    {
        var rows = GraphLayout.Compute(
            [Commit(10, 4), Commit(1, 5, 4, 6), Commit(5, 7), Commit(6, 7), Commit(4, 7), Commit(7)]);

        Assert.Equal(1, rows[1].Lane);
        Assert.Equal(
            [
                new GraphEdge(GraphEdgeKind.Through, 0, 0),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 0),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 2),
            ],
            Shape(rows[1].Edges));
        Assert.Equal(3, rows[1].LaneCount);
    }

    [Fact]
    public void GivenAWaitedForLaneToTheRight_WhenLayingOut_ThenTheEdgeJoinsThatLaneWithoutANewOne()
    {
        // Lane 0 is the node's lane; lane 1 waits for 5, the second parent of 1.
        var rows = GraphLayout.Compute([Commit(10, 1), Commit(11, 5), Commit(1, 2, 5), Commit(2, 5), Commit(5)]);

        Assert.Equal(0, rows[2].Lane);
        Assert.Contains(new GraphEdge(GraphEdgeKind.Outgoing, 0, 1), Shape(rows[2].Edges));
        Assert.Equal(2, rows[2].LaneCount);
    }

    [Fact]
    public void GivenTheBackMerge_WhenValidatingTheTopology_ThenThereAreNoViolations() =>
        Assert.Empty(GraphTopologyValidator.Validate(BackMerge, GraphLayout.Compute(BackMerge)));
}
