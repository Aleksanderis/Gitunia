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
        Assert.Equal([1], node.SkipLanes);
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
    public void GivenABendOverTheCellInTheRowBelow_WhenPlanning_ThenTheArrivingLineIsTheDiagonalAbove()
    {
        var bend = new GraphRow(
            CommitFactory.Id(3),
            2,
            3,
            [new GraphEdge(GraphEdgeKind.Through, 1, 1, 2), new GraphEdge(GraphEdgeKind.Outgoing, 2, 0, 4)]);
        var graph = GraphOf(Passing(1), Junction(2), bend);

        var node = GraphDiagonals.PlanFor(graph, 1, LaneColumns);
        Assert.Empty(node.SkipLanes);
        Assert.Equal([1], node.SkipArrivingLanes);
        Assert.Equal(new DiagonalMark(1, false, 2), Assert.Single(GraphDiagonals.PlanFor(graph, 0, LaneColumns).Marks));
    }

    [Fact]
    public void GivenTheOtherLaneIsInTheLastLaneColumn_WhenPlanning_ThenTheBendStays()
    {
        var graph = GraphOf(Passing(1), Junction(2), Passing(3));

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, 1));
    }

    [Fact]
    public void GivenAnotherMergeIntoTheNode_WhenPlanning_ThenTheJunctionGetsBothDiagonalsAndTheMergeKeepsItsBend()
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

        Assert.Equal([1], plan.SkipLanes);
        Assert.Equal([1], plan.SkipArrivingLanes);
        Assert.Equal("○───╯ ", string.Concat(cells.Select(cell => cell.Glyph)));
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
    public void GivenABendOverTheFarCellInTheRowBelow_WhenDrawing_ThenTheArrivingLineIsTheDiagonalAbove()
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

        Assert.Equal("├ │ ╱ ", Draw(graph, 0, GraphGlyphSet.Unicode));
        Assert.Equal("○───╮ ", Draw(graph, 1, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenADecoratedCommitOnTheFarLaneBelow_WhenPlanning_ThenTheArrivingLineIsTheDiagonalInTheRowAbove()
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

        Assert.Equal(new DiagonalMark(2, false, 2), Assert.Single(GraphDiagonals.PlanFor(graph, 0, LaneColumns).Marks));
        Assert.Equal("├ │ ╱ ", Draw(graph, 0, GraphGlyphSet.Unicode));
        Assert.Equal("○───╮ ", Draw(graph, 1, GraphGlyphSet.Unicode));
        Assert.Equal("│ │ ● ", Draw(graph, 2, GraphGlyphSet.Unicode));
    }

    // The first commit of the new lane 1, with a branch label, right below a junction.
    private static GraphEntry LabelledTip(int number) =>
        new(
            "subject",
            "author",
            DateTimeOffset.UnixEpoch,
            [new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false)],
            new GraphRow(
                CommitFactory.Id(number),
                1,
                2,
                [
                    new GraphEdge(GraphEdgeKind.Incoming, 1, 1, 3),
                    new GraphEdge(GraphEdgeKind.Outgoing, 1, 1, 3),
                    new GraphEdge(GraphEdgeKind.Through, 0, 0, 1),
                ]));

    // The last commit of lane 1, which ends at the junction below it.
    private static GraphRow EndOfLane(int number) =>
        new(
            CommitFactory.Id(number),
            1,
            2,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 1, 1, 2),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 1, 2),
                new GraphEdge(GraphEdgeKind.Through, 0, 0, 1),
            ]);

    [Fact]
    public void GivenALabelledTipRightBelowAJunction_WhenDrawing_ThenThePlainCommitAboveIsTheArrivingDiagonalAndTheDepartureKeepsItsBend()
    {
        var graph = new CommitGraph();
        graph.AddRange(
        [
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], EndOfLane(1)),
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], Junction(2)),
            LabelledTip(3),
        ]);

        var node = GraphDiagonals.PlanFor(graph, 1, LaneColumns);

        Assert.Empty(node.SkipLanes);
        Assert.Equal([1], node.SkipArrivingLanes);
        Assert.Empty(node.Marks);
        Assert.Equal("│ ╱   ", Draw(graph, 0, GraphGlyphSet.Unicode));
        Assert.Equal("○─╮   ", Draw(graph, 1, GraphGlyphSet.Unicode));
        Assert.Equal("│ ●   ", Draw(graph, 2, GraphGlyphSet.Unicode));
        Assert.Equal("| /   ", Draw(graph, 0, GraphGlyphSet.Ascii));
    }

    [Fact]
    public void GivenALabelledCommitAboveAndBelowAJunction_WhenPlanning_ThenBothBendsStay()
    {
        var labelledEnd = new GraphEntry(
            "subject",
            "author",
            DateTimeOffset.UnixEpoch,
            [new CommitDecoration("refs/heads/done", "done", RefKind.LocalBranch, false)],
            EndOfLane(1));
        var graph = new CommitGraph();
        graph.AddRange([labelledEnd, new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], Junction(2)), LabelledTip(3)]);

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 0, LaneColumns));
        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 1, LaneColumns));
        Assert.Equal("○─┤   ", Draw(graph, 1, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenAJunctionOnTheFirstRowWithNoRoomBelow_WhenPlanning_ThenTheBendsStay()
    {
        var graph = new CommitGraph();
        graph.AddRange([new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], Junction(1)), LabelledTip(2)]);

        Assert.Same(RowDiagonals.None, GraphDiagonals.PlanFor(graph, 0, LaneColumns));
    }

    [Fact]
    public void GivenTheCellAboveTakenByADepartingDiagonal_WhenPlanning_ThenTheArrivingLineKeepsItsBend()
    {
        // Junction 1 departs into row 1 (lane 1); junction 2 cannot depart (labelled tip below) and would arrive in row 1 too.
        var graph = new CommitGraph();
        graph.AddRange(
        [
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], Junction(1)),
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], Passing(2)),
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [], Junction(3)),
            LabelledTip(4),
        ]);

        Assert.Equal(new DiagonalMark(1, true, 3), Assert.Single(GraphDiagonals.PlanFor(graph, 1, LaneColumns).Marks));
        Assert.Empty(GraphDiagonals.PlanFor(graph, 2, LaneColumns).SkipArrivingLanes);
        Assert.Equal("○─┤   ", Draw(graph, 2, GraphGlyphSet.Unicode));
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
    public void GivenTwoLanesEndingAndStartingAtTheNode_WhenDrawing_ThenTheOuterKeepsItsCornerAndTheInnerIsDiagonalsOnly()
    {
        var graph = GraphOf(PassingTwo(1), Octopus(2), PassingTwo(3));

        Assert.Equal("├ ╱ │ ", Draw(graph, 0, GraphGlyphSet.Unicode));
        Assert.Equal("○───╯ ", Draw(graph, 1, GraphGlyphSet.Unicode));
        Assert.Equal("├ ╲ ╲ ", Draw(graph, 2, GraphGlyphSet.Unicode));
    }

    // The node on lane 2; lanes 1 and 0 end at it and start again, the farthest lane first in edge order.
    private static GraphRow LeftOctopus(int number) =>
        new(
            CommitFactory.Id(number),
            2,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 2, 2),
                new GraphEdge(GraphEdgeKind.Incoming, 1, 2, 4),
                new GraphEdge(GraphEdgeKind.Incoming, 2, 2, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 0, 3),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 1, 5),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 2, 1),
            ]);

    [Fact]
    public void GivenTwoLanesEndingAndStartingAtTheNode_WhenFindingJunctions_ThenBothAreFound()
    {
        Assert.Equal(
            [new DiagonalJunction(2, 0, 3, 2), new DiagonalJunction(2, 1, 5, 4)],
            GraphDiagonals.JunctionsOf(LeftOctopus(2), LaneColumns));
    }

    [Fact]
    public void GivenThreeLanesEndingAndStartingAtTheNode_WhenFindingJunctions_ThenAllThreeAreFound()
    {
        var wide = new GraphRow(
            CommitFactory.Id(2),
            2,
            5,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 2, 2),
                new GraphEdge(GraphEdgeKind.Incoming, 4, 2, 4),
                new GraphEdge(GraphEdgeKind.Incoming, 3, 2, 6),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 0, 3),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 4, 5),
                new GraphEdge(GraphEdgeKind.Outgoing, 2, 3, 7),
            ]);

        Assert.Equal(
            [new DiagonalJunction(2, 0, 3, 2), new DiagonalJunction(2, 4, 5, 4), new DiagonalJunction(2, 3, 7, 6)],
            GraphDiagonals.JunctionsOf(wide, 5));
    }

    [Fact]
    public void GivenOneLaneOnEachSideEndingAndStartingAtTheNode_WhenDrawing_ThenEachSideGetsItsDiagonal()
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

        Assert.Equal(2, GraphDiagonals.JunctionsOf(node, LaneColumns).Count);
        Assert.Equal("╰─○─╯ ", Draw(graph, 1, GraphGlyphSet.Unicode));
        Assert.Equal("╱ │ ╲ ", Draw(graph, 2, GraphGlyphSet.Unicode));
    }

    // Lane 1 ends at the node on lane 0 and starts again; lane 2 only merges in, its line runs over lane 1's corner.
    private static GraphRow MergeOverJunction(int number) =>
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
            ]);

    private static GraphRow EndOfLaneBesideTwo(int number) =>
        new(
            CommitFactory.Id(number),
            1,
            3,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 1, 1, 2),
                new GraphEdge(GraphEdgeKind.Outgoing, 1, 1, 2),
                new GraphEdge(GraphEdgeKind.Through, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Through, 2, 2, 4),
            ]);

    [Fact]
    public void GivenAFartherMergeRunningOverTheArrivingCorner_WhenDrawing_ThenBothLinesAreDiagonals()
    {
        var graph = GraphOf(EndOfLaneBesideTwo(1), MergeOverJunction(2), Passing(3));

        var node = GraphDiagonals.PlanFor(graph, 1, LaneColumns);

        Assert.Equal([1], node.SkipLanes);
        Assert.Equal([1], node.SkipArrivingLanes);
        Assert.Equal("│ ╱ │ ", Draw(graph, 0, GraphGlyphSet.Unicode));
        Assert.Equal("○───╯ ", Draw(graph, 1, GraphGlyphSet.Unicode));
        Assert.Equal("├ ╲   ", Draw(graph, 2, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenAFartherMergeButNoRoomAbove_WhenDrawing_ThenTheArrivingCornerStaysAJunction()
    {
        var graph = GraphOf(PassingTwo(1), MergeOverJunction(2), Passing(3));
        var labelled = new CommitGraph();
        labelled.AddRange(
        [
            new GraphEntry("subject", "author", DateTimeOffset.UnixEpoch, [new CommitDecoration("refs/heads/done", "done", RefKind.LocalBranch, false)], EndOfLaneBesideTwo(1)),
            graph[1],
            graph[2],
        ]);

        Assert.Empty(GraphDiagonals.PlanFor(labelled, 1, LaneColumns).SkipArrivingLanes);
        Assert.Equal("○─┴─╯ ", Draw(labelled, 1, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenThreeLanesEndingAndStartingAtTheNode_WhenDrawing_ThenTheInnerTwoAreDiagonalsAndTheOuterKeepsItsCorner()
    {
        const int wideColumns = 4;
        var octopus = new GraphRow(
            CommitFactory.Id(2),
            0,
            4,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Incoming, 1, 0, 2),
                new GraphEdge(GraphEdgeKind.Incoming, 2, 0, 4),
                new GraphEdge(GraphEdgeKind.Incoming, 3, 0, 6),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 1, 3),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 2, 5),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 3, 7),
            ]);
        var passing = new GraphRow(
            CommitFactory.Id(1),
            0,
            4,
            [
                new GraphEdge(GraphEdgeKind.Incoming, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Outgoing, 0, 0, 1),
                new GraphEdge(GraphEdgeKind.Through, 1, 1, 2),
                new GraphEdge(GraphEdgeKind.Through, 2, 2, 4),
                new GraphEdge(GraphEdgeKind.Through, 3, 3, 6),
            ]);
        var graph = GraphOf(passing, octopus, passing with { Id = CommitFactory.Id(3) });

        Assert.Equal("├ ╱ ╱ │ ", Draw(graph, 0, GraphGlyphSet.Unicode, wideColumns));
        Assert.Equal("○─────╯ ", Draw(graph, 1, GraphGlyphSet.Unicode, wideColumns));
        Assert.Equal("├ ╲ ╲ ╲ ", Draw(graph, 2, GraphGlyphSet.Unicode, wideColumns));
    }

    private static string Draw(CommitGraph graph, int index, GraphGlyphSet glyphs, int laneColumns = LaneColumns)
    {
        var plan = GraphDiagonals.PlanFor(graph, index, laneColumns);
        var cells = LaneCellBuilder.Build(graph[index].Row, laneColumns, glyphs, graph[index].Decorations.Count > 0, plan);
        return string.Concat(cells.Select(cell => cell.Glyph));
    }
}
