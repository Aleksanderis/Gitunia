using Gitunia.Core.Workspaces;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces;

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
}
