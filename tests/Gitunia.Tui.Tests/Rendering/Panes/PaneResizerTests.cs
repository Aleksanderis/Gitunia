using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Panes;

public class PaneResizerTests
{
    private const int Width = 120;
    private const int Height = 40;

    [Fact]
    public void GivenAutomaticSizes_WhenComputing_ThenTheLayoutEqualsTheOneWithoutSizes()
    {
        Assert.Equal(PaneLayout.Compute(Width, Height, true, null), PaneLayout.Compute(Width, Height, true, null, PaneSizes.Automatic));
    }

    [Fact]
    public void GivenADraggedSidebarDivider_WhenComputing_ThenTheSidebarAndTheGraphFollowIt()
    {
        var sizes = PaneResizer.Drag(PaneSizes.Automatic, PaneEdge.Sidebar, 44, 10, Height);

        var rects = PaneLayout.Compute(Width, Height, false, null, sizes);

        Assert.Equal(43, rects.Sidebar.Width);
        Assert.Equal(44, rects.SidebarDivider.X);
        Assert.Equal(45, rects.Graph.X);
        Assert.Equal(Width - 1 - 45, rects.Graph.Width);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(500, Width - 3 - PaneLayout.MinimumGraphWidth)]
    public void GivenASidebarDraggedPastTheLimits_WhenComputing_ThenItIsClamped(int column, int expected)
    {
        var sizes = PaneResizer.Drag(PaneSizes.Automatic, PaneEdge.Sidebar, column, 10, Height);

        Assert.Equal(expected, PaneLayout.Compute(Width, Height, false, null, sizes).Sidebar.Width);
    }

    [Fact]
    public void GivenADraggedBottomEdge_WhenComputing_ThenTheTitleRowSitsUnderThePointer()
    {
        var sizes = PaneResizer.Drag(PaneSizes.Automatic, PaneEdge.Bottom, 10, 20, Height);

        var rects = PaneLayout.Compute(Width, Height, false, null, sizes);

        Assert.Equal(20, rects.DetailTitle.Y);
        Assert.Equal(ScreenLayout.GraphTop + ScreenLayout.GraphHeight(Height), rects.Detail.Y + rects.Detail.Height);
    }

    [Fact]
    public void GivenTheBottomEdgeDraggedToTheTop_WhenComputing_ThenTheGraphKeepsItsMinimumRows()
    {
        var sizes = PaneResizer.Drag(PaneSizes.Automatic, PaneEdge.Bottom, 10, 0, Height);

        Assert.Equal(PaneLayout.MinimumGraphHeight, PaneLayout.Compute(Width, Height, false, null, sizes).Graph.Height);
    }

    [Fact]
    public void GivenADraggedDetailDivider_WhenComputing_ThenTheDiffTakesTheRest()
    {
        var sizes = PaneResizer.Drag(PaneSizes.Automatic, PaneEdge.Detail, 70, 30, Height);

        var rects = PaneLayout.Compute(Width, Height, true, null, sizes);

        Assert.Equal(69, rects.Detail.Width);
        Assert.Equal(71, rects.Diff.X);
        Assert.Equal(Width - 1, rects.Diff.X + rects.Diff.Width);
    }

    [Fact]
    public void GivenAScreenTooShortForTheBottomPanes_WhenResizing_ThenTheyStayHidden()
    {
        var sizes = PaneResizer.Drag(PaneSizes.Automatic, PaneEdge.Bottom, 10, 3, 10);

        Assert.True(PaneLayout.Compute(Width, 10, false, null, sizes).Detail.IsEmpty);
    }

    [Fact]
    public void GivenAZoomedPane_WhenComputingWithSizes_ThenTheZoomIgnoresThem()
    {
        var sizes = new PaneSizes(20, 8, 30);

        Assert.Equal(PaneLayout.Compute(Width, Height, true, PaneKind.Diff), PaneLayout.Compute(Width, Height, true, PaneKind.Diff, sizes));
    }

    [Theory]
    [InlineData(PaneKind.Sidebar, "pane.wider", 32)]
    [InlineData(PaneKind.Sidebar, "pane.narrower", 28)]
    [InlineData(PaneKind.Graph, "pane.wider", 28)]
    [InlineData(PaneKind.Graph, "pane.narrower", 32)]
    public void GivenTheSidebarOrGraphFocused_WhenResizingByKey_ThenTheSidebarDividerMovesTwoColumns(PaneKind focused, string action, int expectedSidebar)
    {
        var sizes = PaneResizer.Apply(PaneSizes.Automatic, new ActionId(action), null, focused, Width, Height, false);

        Assert.NotNull(sizes);
        Assert.Equal(expectedSidebar, PaneLayout.Compute(Width, Height, false, null, sizes).Sidebar.Width);
    }

    [Fact]
    public void GivenACount_WhenResizingByKey_ThenTheStepIsMultiplied()
    {
        var sizes = PaneResizer.Apply(PaneSizes.Automatic, ActionIds.PaneWider, 3, PaneKind.Sidebar, Width, Height, false);

        Assert.Equal(36, PaneLayout.Compute(Width, Height, false, null, sizes).Sidebar.Width);
    }

    [Theory]
    [InlineData(PaneKind.Detail, "pane.wider", 42)]
    [InlineData(PaneKind.Detail, "pane.narrower", 38)]
    [InlineData(PaneKind.Diff, "pane.wider", 38)]
    [InlineData(PaneKind.Diff, "pane.narrower", 42)]
    public void GivenTheBottomPanesFocusedWithADiff_WhenResizingByKey_ThenTheDetailDividerMoves(PaneKind focused, string action, int expectedDetail)
    {
        var sizes = PaneResizer.Apply(PaneSizes.Automatic, new ActionId(action), null, focused, Width, Height, true);

        Assert.Equal(expectedDetail, PaneLayout.Compute(Width, Height, true, null, sizes).Detail.Width);
    }

    [Fact]
    public void GivenTheDetailPaneSpanningTheScreen_WhenWidening_ThenThereIsNothingToResize()
    {
        Assert.Null(PaneResizer.Apply(PaneSizes.Automatic, ActionIds.PaneWider, null, PaneKind.Detail, Width, Height, false));
    }

    [Theory]
    [InlineData(PaneKind.Detail, "pane.taller", 14)]
    [InlineData(PaneKind.Diff, "pane.shorter", 12)]
    [InlineData(PaneKind.Graph, "pane.taller", 12)]
    [InlineData(PaneKind.Sidebar, "pane.shorter", 14)]
    public void GivenAPaneFocused_WhenResizingHeightByKey_ThenTheBottomEdgeMovesOneRow(PaneKind focused, string action, int expectedBottomHeight)
    {
        var sizes = PaneResizer.Apply(PaneSizes.Automatic, new ActionId(action), null, focused, Width, Height, false);

        Assert.Equal(expectedBottomHeight, PaneLayout.BottomHeight(Height, sizes));
    }

    [Fact]
    public void GivenTheBottomPanesAtTheirMinimum_WhenShrinkingFurther_ThenThereIsNothingToResize()
    {
        var sizes = new PaneSizes(BottomHeight: PaneLayout.MinimumBottomHeight);

        Assert.Null(PaneResizer.Apply(sizes, ActionIds.PaneShorter, null, PaneKind.Detail, Width, Height, false));
    }

    [Fact]
    public void GivenATooShortScreen_WhenResizingHeight_ThenThereIsNothingToResize()
    {
        Assert.Null(PaneResizer.Apply(PaneSizes.Automatic, ActionIds.PaneTaller, null, PaneKind.Detail, Width, 10, false));
    }

    [Fact]
    public void GivenChangedSizes_WhenResetting_ThenTheyAreAutomaticAgain()
    {
        var changed = new PaneSizes(20, 9);

        Assert.Equal(PaneSizes.Automatic, PaneResizer.Apply(changed, ActionIds.PaneResetSizes, null, PaneKind.Graph, Width, Height, false));
        Assert.Null(PaneResizer.Apply(PaneSizes.Automatic, ActionIds.PaneResetSizes, null, PaneKind.Graph, Width, Height, false));
    }

    [Fact]
    public void GivenANarrowScreenWithoutASidebar_WhenWidening_ThenThereIsNothingToResize()
    {
        Assert.Null(PaneResizer.Apply(PaneSizes.Automatic, ActionIds.PaneWider, null, PaneKind.Sidebar, 50, Height, false));
    }
}
