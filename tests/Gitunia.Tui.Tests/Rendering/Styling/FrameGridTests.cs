using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

public class FrameGridTests
{
    private const int Width = 100;
    private const int Height = 30;

    private static FrameGrid Build(PaneKind? focused, bool isDiffOpen = true, GraphGlyphSet? glyphs = null) =>
        FrameGrid.Build(Width, Height, PaneLayout.Compute(Width, Height, isDiffOpen, null), focused, glyphs ?? GraphGlyphSet.Unicode, "repo", "History");

    [Fact]
    public void GivenTheLayout_WhenBuilding_ThenTheOuterCornersAreRoundedAndTheEdgesAreStraight()
    {
        var grid = Build(PaneKind.Graph);

        Assert.Equal("╭", grid.Cell(0, ScreenLayout.FrameTop).Text);
        Assert.Equal("╮", grid.Cell(Width - 1, ScreenLayout.FrameTop).Text);
        Assert.Equal("╰", grid.Cell(0, ScreenLayout.FrameBottom(Height)).Text);
        Assert.Equal("╯", grid.Cell(Width - 1, ScreenLayout.FrameBottom(Height)).Text);
        Assert.Equal("│", grid.Cell(0, ScreenLayout.GraphTop).Text);
        Assert.Equal("─", grid.Cell(Width / 2, ScreenLayout.FrameBottom(Height)).Text);
    }

    [Fact]
    public void GivenSharedEdges_WhenBuilding_ThenJunctionsJoinTheBoxes()
    {
        var rects = PaneLayout.Compute(Width, Height, true, null);
        var grid = Build(PaneKind.Graph);

        Assert.Equal("┬", grid.Cell(rects.SidebarDivider.X, ScreenLayout.FrameTop).Text);
        Assert.Equal("├", grid.Cell(0, rects.DetailTitle.Y).Text);
        Assert.Equal("┤", grid.Cell(Width - 1, rects.DetailTitle.Y).Text);
        Assert.Equal("┬", grid.Cell(rects.BottomDivider.X, rects.DetailTitle.Y).Text);
        Assert.Equal("┴", grid.Cell(rects.BottomDivider.X, ScreenLayout.FrameBottom(Height)).Text);
    }

    [Fact]
    public void GivenAFocusedPane_WhenBuilding_ThenOnlyItsEdgesUseTheAccentRole()
    {
        var rects = PaneLayout.Compute(Width, Height, false, null);
        var sidebarFocused = Build(PaneKind.Sidebar, isDiffOpen: false);
        var graphFocused = Build(PaneKind.Graph, isDiffOpen: false);

        Assert.Equal(SegmentRole.FocusedBorder, sidebarFocused.Cell(0, ScreenLayout.GraphTop).Role);
        Assert.Equal(SegmentRole.Border, graphFocused.Cell(0, ScreenLayout.GraphTop).Role);
        Assert.Equal(SegmentRole.FocusedBorder, graphFocused.Cell(Width - 1, ScreenLayout.GraphTop).Role);
        Assert.Equal(SegmentRole.Border, sidebarFocused.Cell(Width - 1, ScreenLayout.GraphTop).Role);
        Assert.Equal(SegmentRole.FocusedBorder, sidebarFocused.Cell(rects.SidebarDivider.X, ScreenLayout.GraphTop).Role);
        Assert.Equal(SegmentRole.FocusedBorder, graphFocused.Cell(rects.SidebarDivider.X, ScreenLayout.GraphTop).Role);
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenBuilding_ThenTheFrameUsesPlusDashAndBar()
    {
        var grid = Build(PaneKind.Graph, glyphs: GraphGlyphSet.Ascii);

        Assert.Equal("+", grid.Cell(0, ScreenLayout.FrameTop).Text);
        Assert.Equal("|", grid.Cell(0, ScreenLayout.GraphTop).Text);
        Assert.Equal("-", grid.Cell(Width / 2, ScreenLayout.FrameBottom(Height)).Text);
        Assert.Equal("+", grid.Cell(PaneLayout.Compute(Width, Height, true, null).SidebarDivider.X, ScreenLayout.FrameTop).Text);
    }

    [Fact]
    public void GivenTitles_WhenBuilding_ThenTheyStandAsChipsOnTheTopEdgeAfterOneStretchOfLine()
    {
        var grid = Build(PaneKind.Sidebar);

        Assert.Equal("─ repo ─", grid.Row(ScreenLayout.FrameTop, 1, 8).Text);
        Assert.Equal(SegmentRole.ActiveTab, grid.Cell(3, ScreenLayout.FrameTop).Role);
        Assert.Equal(SegmentRole.InactiveTab, grid.Cell(PaneLayout.Compute(Width, Height, true, null).Graph.X + 2, ScreenLayout.FrameTop).Role);
    }

    [Fact]
    public void GivenARuleStretchInATitle_WhenResolving_ThenTheGridCellsReplaceIt()
    {
        var rects = PaneLayout.Compute(Width, Height, false, null);
        var grid = Build(PaneKind.Detail, isDiffOpen: false);
        var title = new StyledLine([new StyledSegment("ab", SegmentRole.Plain), new StyledSegment("──", SegmentRole.Rule)]);

        var resolved = grid.ResolveRules(title, rects.DetailTitle.Y, rects.DetailTitle.X);

        Assert.Equal("ab──", resolved.Text);
        Assert.Equal(SegmentRole.FocusedBorder, resolved.Segments[^1].Role);
    }

    [Theory]
    [InlineData(10, 8)]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    public void GivenATinyScreen_WhenBuilding_ThenNothingThrows(int width, int height)
    {
        var grid = FrameGrid.Build(width, height, PaneLayout.Compute(width, height, false, null), PaneKind.Graph, GraphGlyphSet.Unicode, "repo", "History");

        Assert.Equal(width, TextWidth.Of(grid.Row(0, 0, width).Text));
    }

    [Theory]
    [InlineData("日本語")]
    [InlineData("a🙂b")]
    [InlineData("a-very-long-repository-name-that-does-not-fit-in-the-sidebar-title")]
    public void GivenWideOrLongTitles_WhenBuilding_ThenEveryEdgeRowKeepsTheScreenWidth(string title)
    {
        var rects = PaneLayout.Compute(Width, Height, true, null);
        var grid = FrameGrid.Build(Width, Height, rects, PaneKind.Sidebar, GraphGlyphSet.Unicode, title, title);

        Assert.Equal(Width, TextWidth.Of(grid.Row(ScreenLayout.FrameTop, 0, Width).Text));
        Assert.Equal("╮", grid.Cell(Width - 1, ScreenLayout.FrameTop).Text);
        Assert.Equal("┬", grid.Cell(rects.SidebarDivider.X, ScreenLayout.FrameTop).Text);
        Assert.DoesNotContain(grid.Row(ScreenLayout.FrameTop, 0, Width).Text, character => char.IsSurrogate(character) && !title.Contains(character, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(PaneEdge.Detail)]
    [InlineData(PaneEdge.Bottom)]
    [InlineData(PaneEdge.Sidebar)]
    public void GivenADragOntoTheDrawnLine_WhenComputingTheLayout_ThenTheLineStaysWhereItWasDrawn(PaneEdge edge)
    {
        var rects = PaneLayout.Compute(Width, Height, true, null);
        var (column, row) = edge switch
        {
            PaneEdge.Detail => (rects.BottomDivider.X, rects.BottomDivider.Y + 2),
            PaneEdge.Bottom => (10, rects.DetailTitle.Y),
            _ => (rects.SidebarDivider.X, 5),
        };

        var sizes = PaneResizer.Drag(PaneSizes.Automatic, edge, column, row, Height);

        Assert.Equal(rects, PaneLayout.Compute(Width, Height, true, null, sizes));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(7, 5)]
    [InlineData(30, 6)]
    public void GivenATinyTerminal_WhenComposingAScreen_ThenEveryLineFillsTheWidth(int width, int height)
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "root", []));
        var model = new ScreenModel("日本語", "main", 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty);

        var lines = ScreenComposer.Compose(model, graph, 0, 0, width, height, GraphGlyphSet.Unicode);

        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenAPaneWidth_WhenPadding_ThenTheTextWidthLosesBothSidesAndTheSelectionStaysOnTheLine()
    {
        var line = new StyledLine([new StyledSegment("abc", SegmentRole.Plain)], IsSelected: true);

        var padded = PanePadding.Wrap(line);

        Assert.Equal(" abc ", padded.Text);
        Assert.True(padded.IsSelected);
        Assert.Equal(8, PanePadding.TextWidth(10));
        Assert.Equal(0, PanePadding.TextWidth(1));
    }
}
