using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Panes;

public class PaneLayoutTests
{
    [Fact]
    public void GivenNoDiff_WhenComputing_ThenTheDetailPaneSpansTheBottom()
    {
        var rects = PaneLayout.Compute(120, 40, false, null);

        Assert.Equal(118, rects.Detail.Width);
        Assert.True(rects.Diff.IsEmpty);
        Assert.Equal(DetailLayout.GraphHeight(40), rects.Graph.Height);
        Assert.Equal(rects.Graph.Y + rects.Graph.Height, rects.DetailTitle.Y);
        Assert.Equal(DetailLayout.ContentHeight(40), rects.Detail.Height);
    }

    [Fact]
    public void GivenAWideScreenWithADiff_WhenComputing_ThenDetailAndDiffSitSideBySide()
    {
        var rects = PaneLayout.Compute(140, 40, true, null);

        Assert.False(rects.Detail.IsEmpty);
        Assert.False(rects.Diff.IsEmpty);
        Assert.Equal(rects.Detail.X + rects.Detail.Width, rects.BottomDivider.X);
        Assert.Equal(rects.BottomDivider.X + 1, rects.Diff.X);
        Assert.Equal(139, rects.Diff.X + rects.Diff.Width);
        Assert.Equal(rects.Detail.Y, rects.Diff.Y);
    }

    [Fact]
    public void GivenAVeryNarrowScreenWithADiff_WhenComputing_ThenTheDiffReplacesTheDetailPane()
    {
        var rects = PaneLayout.Compute(71, 40, true, null);

        Assert.True(rects.Detail.IsEmpty);
        Assert.Equal(1, rects.Diff.X);
        Assert.Equal(69, rects.Diff.Width);
    }

    [Theory]
    [InlineData(72, 24)]
    [InlineData(80, 26)]
    [InlineData(100, 33)]
    [InlineData(120, 40)]
    public void GivenADiffAtCommonWidths_WhenComputing_ThenACompactFileListSitsBesideIt(int width, int detailWidth)
    {
        var rects = PaneLayout.Compute(width, 40, true, null);

        Assert.Equal(detailWidth, rects.Detail.Width);
        Assert.Equal(detailWidth + 1, rects.BottomDivider.X);
        Assert.Equal(width - 1, rects.Diff.X + rects.Diff.Width);
        Assert.True(rects.Diff.Width >= 45);
    }

    [Theory]
    [InlineData(PaneKind.Diff)]
    [InlineData(PaneKind.Detail)]
    [InlineData(PaneKind.Graph)]
    public void GivenAZoomedPane_WhenComputing_ThenItTakesTheWholeAreaAndOthersVanish(PaneKind pane)
    {
        var rects = PaneLayout.Compute(100, 30, true, pane);

        var area = ScreenLayout.GraphHeight(30);
        var shown = pane switch
        {
            PaneKind.Diff => rects.Diff,
            PaneKind.Detail => rects.Detail,
            _ => rects.Graph,
        };
        Assert.Equal(98, shown.Width);
        Assert.Equal(area, shown.Height);
        Assert.Equal(1, shown.X);
        Assert.True(rects.Sidebar.IsEmpty);
        Assert.Equal(pane != PaneKind.Graph, rects.Graph.IsEmpty);
        Assert.Equal(pane != PaneKind.Detail, rects.Detail.IsEmpty);
        Assert.Equal(pane != PaneKind.Diff, rects.Diff.IsEmpty);
    }

    [Fact]
    public void GivenAShortScreen_WhenAskingIfADiffCanOpen_ThenZoomIsTheOnlyWay()
    {
        Assert.False(PaneLayout.CanOpenDiff(10));
        Assert.True(PaneLayout.Compute(100, 10, true, null).Diff.IsEmpty);
        Assert.False(PaneLayout.Compute(100, 10, true, PaneKind.Diff).Diff.IsEmpty);
    }

    [Fact]
    public void GivenTheSidebarIsZoomed_WhenComputing_ThenTheNormalLayoutStays()
    {
        Assert.Equal(PaneLayout.Compute(100, 30, false, null), PaneLayout.Compute(100, 30, false, PaneKind.Sidebar));
    }
}
