using Gitunia.Core.Workspaces;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class PaneLayoutTests
{
    [Fact]
    public void GivenNoDiff_WhenComputing_ThenTheDetailPaneSpansTheBottom()
    {
        var rects = PaneLayout.Compute(120, 40, false, null);

        Assert.Equal(120, rects.Detail.Width);
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
        Assert.Equal(rects.Detail.Width, rects.BottomDivider.X);
        Assert.Equal(rects.BottomDivider.X + 1, rects.Diff.X);
        Assert.Equal(140, rects.Diff.X + rects.Diff.Width);
        Assert.Equal(rects.Detail.Y, rects.Diff.Y);
    }

    [Fact]
    public void GivenANarrowScreenWithADiff_WhenComputing_ThenTheDiffReplacesTheDetailPane()
    {
        var rects = PaneLayout.Compute(80, 40, true, null);

        Assert.True(rects.Detail.IsEmpty);
        Assert.Equal(0, rects.Diff.X);
        Assert.Equal(80, rects.Diff.Width);
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
        Assert.Equal(100, shown.Width);
        Assert.Equal(pane == PaneKind.Graph ? area : area - 1, shown.Height);
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
