using Gitunia.Core.Graph;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Graph;

public class GraphDiagonalsTests
{
    private const int LaneColumns = 3;

    private static CommitGraph GraphOf(params GraphRow[] rows)
    {
        var graph = new CommitGraph();
        graph.AddRange([.. rows.Select(row => new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], row))]);
        return graph;
    }

    // Lane 1 ends at the node on lane 0 and a new lane 1 starts there.
    private static GraphRow Junction(int number) =>
        new(
            CommitFactory.Id(number),
            0,
            2,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Incoming, 1, 0, 2),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 1, 3),
            ]);

    private static GraphRow Passing(int number) =>
        new(
            CommitFactory.Id(number),
            0,
            2,
            [new GraphEdge(GraphEdgeKind.Incoming, 0, 0, 1), new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1), new GraphEdge(GraphEdgeKind.Through, 1, 1, 2)]);

    [Fact]
    public void GivenAJunction_WhenPlanning_ThenTheRowBelowGetsTheDiagonalAndTheNodeRowSkipsTheDepartureBend()
    {
        var graph = GraphOf(Passing(1), Junction(2), Passing(3));

        var above = GraphDiagonals.PlanFor(graph, 0, LaneColumns);
        var node = GraphDiagonals.PlanFor(graph, 1, LaneColumns);
        var below = GraphDiagonals.PlanFor(graph, 2, LaneColumns);

        Assert.Same(RowDiagonals.None, above);
        Assert.Equal(1, node.SkipLane);
        Assert.Empty(node.Marks);
        Assert.Equal(new DiagonalMark(1, true, 3), Assert.Single(below.Marks));
    }

    [Fact]
    public void GivenAJunctionOnTheLastRow_WhenPlanning_ThenTheBendStays()
    {
        var graph = GraphOf(Passing(1), Passing(2), Junction(3));

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 2, LaneColumns));
    }

    [Fact]
    public void GivenABendOverTheCellInTheRowBelow_WhenPlanning_ThenTheBendStays()
    {
        var bend = new GraphRow(
            CommitFactory.Id(3),
            2,
            3,
            [new GraphEdge(GraphEdgeKind.Through, 1, 1, 2), new GraphEdge(GraphEdgeKind.Outgoing, 2, 0, 4)]);
        var graph = GraphOf(Passing(1), Junction(2), bend);

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, LaneColumns));
    }

    [Fact]
    public void GivenTheOtherLaneIsInTheLastLaneColumn_WhenPlanning_ThenTheBendStays()
    {
        var graph = GraphOf(Passing(1), Junction(2), Passing(3));

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, 1));
    }

    [Fact]
    public void GivenAnotherMergeIntoTheNode_WhenPlanning_ThenTheJunctionStillGetsItsDiagonalAndTheMergeKeepsItsBend()
    {
        var withMerge = new GraphRow(
            CommitFactory.Id(2),
            0,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Incoming, 1, 0, 2),
                new GraphEdge(GraphEdgeKind.Incoming, 2, 0, 4),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 1, 3),
            ]);
        var graph = GraphOf(Passing(1), withMerge, Passing(3));

        var plan = GraphDiagonals.PlanFor(graph, 1, LaneColumns);
        var cells = LaneCellBuilder.Build(withMerge, LaneColumns, GraphGlyphSet.Unicode, true, plan);

        Assert.Equal(1, plan.SkipLane);
        Assert.Equal("○─┴─╯ ", string.Concat(cells.Select(cell => cell.Glyph)));
    }
}
