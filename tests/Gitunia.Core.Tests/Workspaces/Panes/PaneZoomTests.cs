using Gitunia.Core.Workspaces.Panes;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Panes;

[Trait("Os", "Independent")]
public class PaneZoomTests
{
    [Theory]
    [InlineData(PaneKind.Graph, true)]
    [InlineData(PaneKind.Detail, true)]
    [InlineData(PaneKind.Diff, true)]
    [InlineData(PaneKind.Sidebar, false)]
    public void GivenAPane_WhenAskingIfItCanZoom_ThenOnlyTheMainPanesCan(PaneKind pane, bool expected)
    {
        Assert.Equal(expected, PaneZoom.CanZoom(pane));
    }

    [Fact]
    public void GivenNoZoom_WhenToggling_ThenTheFocusedPaneZooms()
    {
        Assert.Equal(PaneKind.Diff, PaneZoom.Toggle(null, PaneKind.Diff));
    }

    [Fact]
    public void GivenTheFocusedPaneIsZoomed_WhenToggling_ThenZoomEnds()
    {
        Assert.Null(PaneZoom.Toggle(PaneKind.Diff, PaneKind.Diff));
    }

    [Theory]
    [InlineData(PaneKind.Detail, PaneKind.Diff, true)]
    [InlineData(PaneKind.Diff, PaneKind.Detail, true)]
    [InlineData(PaneKind.Diff, PaneKind.Diff, true)]
    [InlineData(PaneKind.Graph, PaneKind.Diff, false)]
    [InlineData(PaneKind.Detail, PaneKind.Graph, false)]
    public void GivenTwoPanes_WhenPairing_ThenOnlyDetailAndDiffDo(PaneKind a, PaneKind b, bool expected)
    {
        Assert.Equal(expected, PaneZoom.IsZoomPair(a, b));
    }
}
