using Gitunia.Core.Workspaces.Panes;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Panes;

public class PaneNavigatorTests
{
    private static readonly PaneKind[] SidebarAndGraph = [PaneKind.Sidebar, PaneKind.Graph];
    private static readonly PaneKind[] WorkingLists = [PaneKind.WorkingUnstaged, PaneKind.WorkingStaged];
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

    [Theory]
    [InlineData(PaneKind.Graph, true, PaneKind.Detail)]
    [InlineData(PaneKind.Sidebar, true, PaneKind.Detail)]
    [InlineData(PaneKind.Detail, false, PaneKind.Graph)]
    [InlineData(PaneKind.Diff, false, PaneKind.Graph)]
    [InlineData(PaneKind.Graph, false, PaneKind.Graph)]
    [InlineData(PaneKind.Detail, true, PaneKind.Detail)]
    public void GivenAllPanes_WhenSteppingVertically_ThenFocusMovesBetweenTheTopAndBottomRows(PaneKind current, bool down, PaneKind expected) =>
        Assert.Equal(expected, PaneNavigator.StepVertically(current, down, AllPanes));

    [Theory]
    [InlineData(PaneKind.WorkingUnstaged, true, PaneKind.WorkingStaged)]
    [InlineData(PaneKind.WorkingStaged, false, PaneKind.WorkingUnstaged)]
    [InlineData(PaneKind.WorkingUnstaged, false, PaneKind.WorkingUnstaged)]
    [InlineData(PaneKind.WorkingStaged, true, PaneKind.WorkingStaged)]
    public void GivenTheWorkingTreeLists_WhenSteppingVertically_ThenUnstagedIsAboveStaged(PaneKind current, bool down, PaneKind expected) =>
        Assert.Equal(expected, PaneNavigator.StepVertically(current, down, WorkingLists));

    [Fact]
    public void GivenTheWorkingTreeLists_WhenCycling_ThenTheTwoListsAlternate()
    {
        Assert.Equal(PaneKind.WorkingStaged, PaneNavigator.Cycle(PaneKind.WorkingUnstaged, true, WorkingLists));
        Assert.Equal(PaneKind.WorkingUnstaged, PaneNavigator.Cycle(PaneKind.WorkingStaged, true, WorkingLists));
        Assert.Equal(PaneKind.WorkingStaged, PaneNavigator.Cycle(PaneKind.WorkingUnstaged, false, WorkingLists));
    }

    [Theory]
    [InlineData(PaneKind.WorkingUnstagedDiff, true)]
    [InlineData(PaneKind.WorkingUnstagedDiff, false)]
    [InlineData(PaneKind.WorkingStagedDiff, true)]
    [InlineData(PaneKind.WorkingStagedDiff, false)]
    public void GivenAWorkingTreeDiff_WhenSteppingVertically_ThenFocusStays(PaneKind current, bool down) =>
        Assert.Equal(current, PaneNavigator.StepVertically(current, down, WorkingLists));

    [Fact]
    public void GivenTheListsAndTheDiffOfTheSelectedSide_WhenCycling_ThenTheDiffComesAfterTheLists()
    {
        PaneKind[] cycle = [.. WorkingLists, PaneKind.WorkingStagedDiff];

        Assert.Equal(PaneKind.WorkingStagedDiff, PaneNavigator.Cycle(PaneKind.WorkingStaged, true, cycle));
        Assert.Equal(PaneKind.WorkingUnstaged, PaneNavigator.Cycle(PaneKind.WorkingStagedDiff, true, cycle));
        Assert.Equal(PaneKind.WorkingStagedDiff, PaneNavigator.Cycle(PaneKind.WorkingUnstaged, false, cycle));
    }

    [Fact]
    public void GivenOnlyTheDiffBelow_WhenSteppingDown_ThenTheDiffIsFocused() =>
        Assert.Equal(PaneKind.Diff, PaneNavigator.StepVertically(PaneKind.Graph, true, [PaneKind.Graph, PaneKind.Diff]));

    [Fact]
    public void GivenNoBottomPane_WhenSteppingDown_ThenFocusStays() =>
        Assert.Equal(PaneKind.Graph, PaneNavigator.StepVertically(PaneKind.Graph, true, SidebarAndGraph));

    [Fact]
    public void GivenNoGraph_WhenSteppingUp_ThenTheSidebarIsFocused() =>
        Assert.Equal(PaneKind.Sidebar, PaneNavigator.StepVertically(PaneKind.Detail, false, [PaneKind.Sidebar, PaneKind.Detail]));
}
