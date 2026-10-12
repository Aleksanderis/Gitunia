using Gitunia.Core.Workspaces;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces;

public class PaneNavigatorTests
{
    private static readonly PaneKind[] SidebarAndGraph = [PaneKind.Sidebar, PaneKind.Graph];
    private static readonly PaneKind[] AllPanes = [PaneKind.Sidebar, PaneKind.Graph, PaneKind.Detail, PaneKind.Diff];

    [Theory]
    [InlineData(PaneKind.Sidebar, true, PaneKind.Graph)]
    [InlineData(PaneKind.Graph, true, PaneKind.Sidebar)]
    [InlineData(PaneKind.Sidebar, false, PaneKind.Graph)]
    [InlineData(PaneKind.Graph, false, PaneKind.Sidebar)]
    public void GivenSidebarAndGraph_WhenCycling_ThenItWrapsBothWays(PaneKind current, bool forward, PaneKind expected) =>
        Assert.Equal(expected, PaneNavigator.Cycle(current, forward, SidebarAndGraph));

    [Fact]
    public void GivenAllPanes_WhenCyclingBackwardFromSidebar_ThenDiffIsNext() =>
        Assert.Equal(PaneKind.Diff, PaneNavigator.Cycle(PaneKind.Sidebar, false, AllPanes));

    [Fact]
    public void GivenClosedFocusedPane_WhenCycling_ThenFirstOpenPaneIsFocused() =>
        Assert.Equal(PaneKind.Graph, PaneNavigator.Cycle(PaneKind.Sidebar, true, [PaneKind.Graph]));

    [Fact]
    public void GivenGraphOnly_WhenCycling_ThenFocusStays() =>
        Assert.Equal(PaneKind.Graph, PaneNavigator.Cycle(PaneKind.Graph, true, [PaneKind.Graph]));

    [Theory]
    [InlineData(PaneKind.Graph, false, PaneKind.Sidebar)]
    [InlineData(PaneKind.Sidebar, true, PaneKind.Graph)]
    [InlineData(PaneKind.Sidebar, false, PaneKind.Sidebar)]
    [InlineData(PaneKind.Graph, true, PaneKind.Graph)]
    public void GivenSidebarAndGraph_WhenStepping_ThenFocusStopsAtTheEdges(PaneKind current, bool toRight, PaneKind expected) =>
        Assert.Equal(expected, PaneNavigator.Step(current, toRight, SidebarAndGraph));
}
