using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
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

    // Lane 2 ends at the node on lane 0 and a new lane 2 starts there; lane 1 runs through the row.
    private static GraphRow FarJunction(int number) =>
        new(
            CommitFactory.Id(number),
            0,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Incoming, 2, 0, 2),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 2, 3),
                new GraphEdge(GraphEdgeKind.Through, 1, 1, 4),
            ]);

    private static GraphRow PassingTwo(int number) =>
        new(
            CommitFactory.Id(number),
            0,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Through, 1, 1, 4),
                new GraphEdge(GraphEdgeKind.Through, 2, 2, 3),
            ]);

    [Fact]
    public void GivenALaneTwoAwayEndingAndStartingAtTheNode_WhenDrawing_ThenTheHorizontalRunEndsInTheBendAndTheDiagonalIsBelowIt()
    {
        var graph = GraphOf(PassingTwo(1), FarJunction(2), PassingTwo(3));

        var node = GraphDiagonals.PlanFor(graph, 1, LaneColumns);
        var below = GraphDiagonals.PlanFor(graph, 2, LaneColumns);
        var nodeCells = LaneCellBuilder.Build(graph[1].Row, LaneColumns, GraphGlyphSet.Unicode, true, node);
        var belowCells = LaneCellBuilder.Build(graph[2].Row, LaneColumns, GraphGlyphSet.Unicode, false, below);

        Assert.Equal("○───╯ ", string.Concat(nodeCells.Select(cell => cell.Glyph)));
        Assert.Equal("├ │ ╲ ", string.Concat(belowCells.Select(cell => cell.Glyph)));
    }

    [Fact]
    public void GivenALaneTwoAwayInTheAsciiSet_WhenDrawing_ThenTheDiagonalIsABackslash()
    {
        var graph = GraphOf(PassingTwo(1), FarJunction(2), PassingTwo(3));

        var below = GraphDiagonals.PlanFor(graph, 2, LaneColumns);
        var belowCells = LaneCellBuilder.Build(graph[2].Row, LaneColumns, GraphGlyphSet.Ascii, false, below);

        Assert.Equal("| | \\ ", string.Concat(belowCells.Select(cell => cell.Glyph)));
    }

    [Fact]
    public void GivenTheFarLaneIsHiddenInTheLastColumn_WhenPlanning_ThenTheBendStays()
    {
        var graph = GraphOf(PassingTwo(1), FarJunction(2), PassingTwo(3));

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, 2));
    }

    [Fact]
    public void GivenALaneThatOnlyEndsAtTheNode_WhenPlanning_ThenTheRoundedCornerStays()
    {
        var onlyEnds = new GraphRow(
            CommitFactory.Id(2),
            0,
            2,
            [new GraphEdge(GraphEdgeKind.Incoming, 1, 0, 2), new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1)]);
        var graph = GraphOf(Passing(1), onlyEnds, Passing(3));

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, LaneColumns));
        Assert.Equal("●─╯   ", Draw(graph, 1, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenALaneThatOnlyStartsAtTheNode_WhenPlanning_ThenTheRoundedCornerStays()
    {
        var onlyStarts = new GraphRow(
            CommitFactory.Id(2),
            0,
            2,
            [new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1), new GraphEdge(GraphEdgeKind.Outgoing, 0, 1, 3)]);
        var graph = GraphOf(Passing(1), onlyStarts, Passing(3));

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, LaneColumns));
        Assert.Equal("○─╮   ", Draw(graph, 1, GraphGlyphSet.Unicode));
    }

    // The node on lane 2; lane 0 ends there and starts again; lane 1 runs through the row.
    private static GraphRow LeftJunction(int number) =>
        new(
            CommitFactory.Id(number),
            2,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 2, 2, 1),
                new GraphEdge(GraphEdgeKind.Incoming, 0, 2, 2),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 2, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 0, 3),
                new GraphEdge(GraphEdgeKind.Through, 1, 1, 4),
            ]);

    private static GraphRow PassingLeft(int number) =>
        new(
            CommitFactory.Id(number),
            2,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 2, 2, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 2, 1),
                new GraphEdge(GraphEdgeKind.Through, 0, 0, 3),
                new GraphEdge(GraphEdgeKind.Through, 1, 1, 4),
            ]);

    [Fact]
    public void GivenAFarLaneToTheLeft_WhenDrawing_ThenTheRunEndsInTheBendAndTheDiagonalFallsToTheLeft()
    {
        var graph = GraphOf(PassingLeft(1), LeftJunction(2), PassingLeft(3));

        Assert.Equal("╰───○ ", Draw(graph, 1, GraphGlyphSet.Unicode));
        Assert.Equal("╱ │ ├ ", Draw(graph, 2, GraphGlyphSet.Unicode));
        Assert.Equal("/ | | ", Draw(graph, 2, GraphGlyphSet.Ascii));
    }

    [Fact]
    public void GivenAFarLaneInTheLastColumn_WhenPlanning_ThenTheDiagonalFitsAndOneColumnLessKeepsTheBend()
    {
        var graph = GraphOf(PassingTwo(1), FarJunction(2), PassingTwo(3));

        Assert.Equal(new DiagonalMark(2, true, 3), Assert.Single(GraphDiagonals.PlanFor(graph, 2, 3).Marks));
        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 2, 2));
    }

    [Fact]
    public void GivenABendOverTheFarCellInTheRowBelow_WhenPlanning_ThenTheBendStays()
    {
        var bend = new GraphRow(
            CommitFactory.Id(3),
            0,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 2, 0, 5),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Through, 1, 1, 4),
            ]);
        var graph = GraphOf(PassingTwo(1), FarJunction(2), bend);

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, LaneColumns));
        Assert.Equal("○───┤ ", Draw(graph, 1, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenADecoratedCommitOnTheFarLaneBelow_WhenPlanning_ThenTheBendStays()
    {
        var tip = new GraphRow(
            CommitFactory.Id(3),
            2,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 2, 2, 3),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 2, 3),
                new GraphEdge(GraphEdgeKind.Through, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Through, 1, 1, 4),
            ]);
        var branch = new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false);
        var graph = new CommitGraph();
        graph.AddRange(
        [
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], PassingTwo(1)),
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], FarJunction(2)),
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [branch], tip),
        ]);

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, LaneColumns));
    }

    // Lanes 1 and 2 end at the node on lane 0 and start again (an octopus merge into freed lanes).
    private static GraphRow Octopus(int number) =>
        new(
            CommitFactory.Id(number),
            0,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Incoming, 1, 0, 2),
                new GraphEdge(GraphEdgeKind.Incoming, 2, 0, 4),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 1, 3),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 2, 5),
            ]);

    [Fact]
    public void GivenTwoLanesEndingAndStartingAtTheNode_WhenDrawing_ThenOnlyTheNearestGetsTheDiagonal()
    {
        var graph = GraphOf(PassingTwo(1), Octopus(2), PassingTwo(3));

        Assert.Equal(new DiagonalJunction(0, 1, 3), GraphDiagonals.JunctionOf(graph[1].Row, LaneColumns));
        Assert.Equal("○─┴─┤ ", Draw(graph, 1, GraphGlyphSet.Unicode));
        Assert.Equal("├ ╲ │ ", Draw(graph, 2, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenTwoLanesAtTheSameDistanceEndingAndStartingAtTheNode_WhenPlanning_ThenBothKeepTheirBends()
    {
        var node = new GraphRow(
            CommitFactory.Id(2),
            1,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 1, 1, 1),
                new GraphEdge(GraphEdgeKind.Incoming, 0, 1, 2),
                new GraphEdge(GraphEdgeKind.Incoming, 2, 1, 4),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 1, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 0, 3),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 2, 5),
            ]);
        var graph = GraphOf(PassingTwo(1), node, PassingTwo(3));

        Assert.Null(GraphDiagonals.JunctionOf(node, LaneColumns));
        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, LaneColumns));
    }

    private static string Draw(CommitGraph graph, int index, GraphGlyphSet glyphs)
    {
        var isNodeRow = graph[index].Row.Edges.Any(edge => edge.Kind == GraphEdgeKind.Incoming) && graph[index].Row.Edges.Any(edge => edge.Kind == GraphEdgeKind.Outgoing && edge.ToLane != graph[index].Row.Lane);
        var plan = GraphDiagonals.PlanFor(graph, index, LaneColumns);
        var cells = LaneCellBuilder.Build(graph[index].Row, LaneColumns, glyphs, isNodeRow, plan);
        return string.Concat(cells.Select(cell => cell.Glyph));
    }
}
