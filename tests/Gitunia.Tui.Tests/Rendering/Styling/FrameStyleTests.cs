using Gitunia.Core.Config.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

public class FrameStyleTests
{
    private const int Width = 100;
    private const int Height = 30;

    private static FrameGrid Build(GraphGlyphSet glyphs, out PaneRects rects)
    {
        rects = PaneLayout.Compute(Width, Height, true, null);
        return FrameGrid.Build(Width, Height, rects, PaneKind.Graph, glyphs, "repo", "History");
    }

    [Fact]
    public void GivenSquareStyle_WhenDrawingTheFrame_ThenTheFourOuterCornersAreSquare()
    {
        var grid = Build(GraphGlyphSet.Unicode with { Frame = FrameStyle.Square }, out _);
        var bottom = ScreenLayout.FrameBottom(Height);

        Assert.Equal("┌", grid.Cell(0, ScreenLayout.FrameTop).Text);
        Assert.Equal("┐", grid.Cell(Width - 1, ScreenLayout.FrameTop).Text);
        Assert.Equal("└", grid.Cell(0, bottom).Text);
        Assert.Equal("┘", grid.Cell(Width - 1, bottom).Text);
    }

    [Fact]
    public void GivenRoundedStyle_WhenDrawingTheFrame_ThenTheCornersAreRounded()
    {
        var grid = Build(GraphGlyphSet.Unicode, out _);

        Assert.Equal("╭", grid.Cell(0, ScreenLayout.FrameTop).Text);
        Assert.Equal("╯", grid.Cell(Width - 1, ScreenLayout.FrameBottom(Height)).Text);
    }

    [Fact]
    public void GivenSquareStyle_WhenDrawingJunctions_ThenTheyAreUnchanged()
    {
        var grid = Build(GraphGlyphSet.Unicode with { Frame = FrameStyle.Square }, out var rects);

        Assert.Equal("┬", grid.Cell(rects.SidebarDivider.X, ScreenLayout.FrameTop).Text);
    }

    [Fact]
    public void GivenSquareStyleOnAnAsciiTerminal_WhenDrawingTheFrame_ThenAsciiStillApplies()
    {
        var glyphs = GraphGlyphSet.Ascii with { Frame = FrameStyle.Square };
        var grid = Build(glyphs, out _);

        Assert.True(glyphs.IsAscii);
        Assert.Equal("+", grid.Cell(0, ScreenLayout.FrameTop).Text);
    }

    [Theory]
    [InlineData(FrameStyle.Rounded, "╰", "╯")]
    [InlineData(FrameStyle.Square, "└", "┘")]
    public void GivenAFrameStyle_WhenDrawingAnOverlayBottom_ThenCornersFollow(FrameStyle style, string left, string right)
    {
        var text = OverlayBox.Bottom(20, GraphGlyphSet.Unicode with { Frame = style }).Text;

        Assert.StartsWith(left, text, StringComparison.Ordinal);
        Assert.EndsWith(right, text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenSquareStyle_WhenUsingTheGraphGlyphs_ThenTheGraphCurvesStayRounded()
    {
        var glyphs = GraphGlyphSet.Unicode with { Frame = FrameStyle.Square };

        Assert.Equal('╰', glyphs.For(LineDirections.Up | LineDirections.Right));
    }
}
