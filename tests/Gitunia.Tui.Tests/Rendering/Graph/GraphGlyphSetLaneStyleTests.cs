using Gitunia.Core.Config.Models;
using Gitunia.Core.Graph;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Graph;

public class GraphGlyphSetLaneStyleTests
{
    private static GraphEdge[] Merge() =>
    [
        new GraphEdge(GraphEdgeKind.Through, 0, 0),
        new GraphEdge(GraphEdgeKind.Incoming, 1, 1),
        new GraphEdge(GraphEdgeKind.Incoming, 2, 1),
    ];

    private static string Draw(GraphGlyphSet glyphs, params GraphEdge[] edges)
    {
        var row = new GraphRow(CommitFactory.Id(1), 1, 3, edges);

        return string.Concat(LaneCellBuilder.Build(row, 3, glyphs, true).Select(cell => cell.Glyph));
    }

    [Fact]
    public void GivenTheLightStyle_WhenDrawingALane_ThenTheLightRoundedGlyphsAreUsed()
    {
        var glyphs = GraphGlyphSet.Unicode.WithLaneStyle(LaneStyle.Light);

        Assert.Equal("│ ●─╯", Draw(glyphs, Merge()).TrimEnd());
    }

    [Fact]
    public void GivenTheHeavyStyle_WhenDrawingALane_ThenHeavyLinesWithSquareCornersAreUsed()
    {
        var glyphs = GraphGlyphSet.Unicode.WithLaneStyle(LaneStyle.Heavy);

        Assert.Equal("┃ ●━┛", Draw(glyphs, Merge()).TrimEnd());
    }

    [Fact]
    public void GivenTheHeavyStyle_WhenAskingForFrameLines_ThenTheyStayLight()
    {
        var glyphs = GraphGlyphSet.Unicode.WithLaneStyle(LaneStyle.Heavy);

        Assert.Equal('─', glyphs.Horizontal);
        Assert.Equal('│', glyphs.For(LineDirections.Up | LineDirections.Down));
        Assert.False(glyphs.IsAscii);
    }

    [Fact]
    public void GivenTheAsciiSet_WhenAskingForAHeavyStyle_ThenNothingChanges()
    {
        Assert.Same(GraphGlyphSet.Ascii, GraphGlyphSet.Ascii.WithLaneStyle(LaneStyle.Heavy));
    }

    [Fact]
    public void GivenAHeavyStyleThenLight_WhenSwitching_ThenTheOriginalSetIsBack()
    {
        var back = GraphGlyphSet.Unicode.WithLaneStyle(LaneStyle.Heavy).WithLaneStyle(LaneStyle.Light);

        Assert.Equal(GraphGlyphSet.Unicode, back);
    }
}
