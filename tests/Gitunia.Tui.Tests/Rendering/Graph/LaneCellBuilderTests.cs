using Gitunia.Core.Graph;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Graph;

public class LaneCellBuilderTests
{
    private static string Draw(GraphGlyphSet glyphs, int laneColumns, int nodeLane, params GraphEdge[] edges)
    {
        return DrawWith(glyphs, laneColumns, nodeLane, false, edges);
    }

    private static string DrawWith(GraphGlyphSet glyphs, int laneColumns, int nodeLane, bool isDecorated, params GraphEdge[] edges)
    {
        var row = new GraphRow(CommitFactory.Id(1), nodeLane, laneColumns, edges);
        return new string([.. LaneCellBuilder.Build(row, laneColumns, glyphs, isDecorated).Select(cell => cell.Glyph)]);
    }

    [Fact]
    public void GivenOctopusMerge_WhenDrawing_ThenInnerParentLaneIsATeeDown()
    {
        var edges = new[]
        {
            new GraphEdge(GraphEdgeKind.Outgoing, 0, 0),
            new GraphEdge(GraphEdgeKind.Outgoing, 0, 1),
            new GraphEdge(GraphEdgeKind.Outgoing, 0, 2),
        };

        Assert.Equal("○─┬─╮ ", Draw(GraphGlyphSet.Unicode, 3, 0, edges));
        Assert.Equal("o---\\ ", Draw(GraphGlyphSet.Ascii, 3, 0, edges));
    }

    [Fact]
    public void GivenLaneEndingUnderAHorizontal_WhenDrawing_ThenInnerLaneIsATeeUp()
    {
        var edges = new[]
        {
            new GraphEdge(GraphEdgeKind.Incoming, 1, 0),
            new GraphEdge(GraphEdgeKind.Outgoing, 0, 2),
        };

        Assert.Equal("●─┴─╮ ", Draw(GraphGlyphSet.Unicode, 3, 0, edges));
    }

    [Fact]
    public void GivenThroughLaneAndLaneEndingAtTheNode_WhenDrawing_ThenNothingCutsTheThroughLane()
    {
        var edges = new[]
        {
            new GraphEdge(GraphEdgeKind.Through, 0, 0),
            new GraphEdge(GraphEdgeKind.Outgoing, 1, 0),
        };

        Assert.Equal("├─● ", Draw(GraphGlyphSet.Unicode, 2, 1, edges));
    }

    [Fact]
    public void GivenLeftwardLaneEndingAndReusedForAParent_WhenDrawing_ThenOneTee()
    {
        var edges = new[]
        {
            new GraphEdge(GraphEdgeKind.Incoming, 0, 1),
            new GraphEdge(GraphEdgeKind.Outgoing, 1, 0),
        };

        Assert.Equal("├─● ", Draw(GraphGlyphSet.Unicode, 2, 1, edges));
    }

    [Fact]
    public void GivenCommitWithOneChildAndOneParentOnItsLane_WhenDrawing_ThenItIsPartOfTheLine()
    {
        var edges = new[]
        {
            new GraphEdge(GraphEdgeKind.Incoming, 0, 0),
            new GraphEdge(GraphEdgeKind.Outgoing, 0, 0),
        };

        Assert.Equal("├ ", Draw(GraphGlyphSet.Unicode, 1, 0, edges));
        Assert.Equal("| ", Draw(GraphGlyphSet.Ascii, 1, 0, edges));
    }

    [Fact]
    public void GivenDecoratedCommitInTheMiddleOfALine_WhenDrawing_ThenItKeepsANodeDot()
    {
        var edges = new[]
        {
            new GraphEdge(GraphEdgeKind.Incoming, 0, 0),
            new GraphEdge(GraphEdgeKind.Outgoing, 0, 0),
        };

        Assert.Equal("● ", DrawWith(GraphGlyphSet.Unicode, 1, 0, true, edges));
        Assert.Equal("* ", DrawWith(GraphGlyphSet.Ascii, 1, 0, true, edges));
    }

    [Fact]
    public void GivenBranchTipAndRoot_WhenDrawing_ThenTheyStayNodes()
    {
        var tip = new[] { new GraphEdge(GraphEdgeKind.Outgoing, 0, 0) };
        var root = new[] { new GraphEdge(GraphEdgeKind.Incoming, 0, 0) };

        Assert.Equal("● ", Draw(GraphGlyphSet.Unicode, 1, 0, tip));
        Assert.Equal("● ", Draw(GraphGlyphSet.Unicode, 1, 0, root));
        Assert.Equal("* ", Draw(GraphGlyphSet.Ascii, 1, 0, root));
    }

    [Fact]
    public void GivenCommitWhereAnotherLaneJoins_WhenDrawing_ThenItStaysANode()
    {
        var edges = new[]
        {
            new GraphEdge(GraphEdgeKind.Incoming, 0, 0),
            new GraphEdge(GraphEdgeKind.Incoming, 1, 0),
            new GraphEdge(GraphEdgeKind.Outgoing, 0, 0),
        };

        Assert.Equal("●─╯ ", Draw(GraphGlyphSet.Unicode, 2, 0, edges));
    }
}
