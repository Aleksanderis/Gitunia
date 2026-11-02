using Gitunia.Core.Workspaces.Panes;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Panes;

[Trait("Os", "Independent")]
public class PaneNavigatorTests
{
    private static readonly PaneKind[] SidebarAndGraph = [PaneKind.Sidebar, PaneKind.Graph];

    [Theory]
    [InlineData(PaneKind.Sidebar, true, PaneKind.Graph, new[] { PaneKind.Sidebar, PaneKind.Graph })]
    [InlineData(PaneKind.Graph, true, PaneKind.Sidebar, new[] { PaneKind.Sidebar, PaneKind.Graph })]
    [InlineData(PaneKind.Sidebar, false, PaneKind.Graph, new[] { PaneKind.Sidebar, PaneKind.Graph })]
    [InlineData(PaneKind.Graph, false, PaneKind.Sidebar, new[] { PaneKind.Sidebar, PaneKind.Graph })]
    [InlineData(
        PaneKind.Sidebar,
        false,
        PaneKind.Diff,
        new[] { PaneKind.Sidebar, PaneKind.Graph, PaneKind.Detail, PaneKind.Diff })] // Backward from the first wraps.
    [InlineData(PaneKind.Sidebar, true, PaneKind.Graph, new[] { PaneKind.Graph })] // The focused pane is closed.
    [InlineData(PaneKind.Graph, true, PaneKind.Graph, new[] { PaneKind.Graph })] // One pane: the focus stays.
    [InlineData(
        PaneKind.WorkingUnstaged,
        true,
        PaneKind.WorkingStaged,
        new[] { PaneKind.WorkingUnstaged, PaneKind.WorkingStaged })] // The working lists alternate.
    [InlineData(
        PaneKind.WorkingStaged,
        true,
        PaneKind.WorkingUnstaged,
        new[] { PaneKind.WorkingUnstaged, PaneKind.WorkingStaged })]
    [InlineData(
        PaneKind.WorkingUnstaged,
        false,
        PaneKind.WorkingStaged,
        new[] { PaneKind.WorkingUnstaged, PaneKind.WorkingStaged })]
    [InlineData(
        PaneKind.WorkingStaged,
        true,
        PaneKind.WorkingStagedDiff,
        new[] { PaneKind.WorkingUnstaged, PaneKind.WorkingStaged, PaneKind.WorkingStagedDiff })] // The diff follows.
    [InlineData(
        PaneKind.WorkingStagedDiff,
        true,
        PaneKind.WorkingUnstaged,
        new[] { PaneKind.WorkingUnstaged, PaneKind.WorkingStaged, PaneKind.WorkingStagedDiff })]
    [InlineData(
        PaneKind.WorkingUnstaged,
        false,
        PaneKind.WorkingStagedDiff,
        new[] { PaneKind.WorkingUnstaged, PaneKind.WorkingStaged, PaneKind.WorkingStagedDiff })]
    public void GivenOpenPanes_WhenCycling_ThenItWrapsBothWaysOverTheOpenOnes(
        PaneKind current,
        bool forward,
        PaneKind expected,
        PaneKind[] open) =>
        Assert.Equal(expected, PaneNavigator.Cycle(current, forward, open));

    [Theory]
    [InlineData(PaneKind.Graph, false, PaneKind.Sidebar)]
    [InlineData(PaneKind.Sidebar, true, PaneKind.Graph)]
    [InlineData(PaneKind.Sidebar, false, PaneKind.Sidebar)]
    [InlineData(PaneKind.Graph, true, PaneKind.Graph)]
    public void GivenSidebarAndGraph_WhenStepping_ThenFocusStopsAtTheEdges(
        PaneKind current,
        bool toRight,
        PaneKind expected) =>
        Assert.Equal(expected, PaneNavigator.Step(current, toRight, SidebarAndGraph));

    [Theory]
    [MemberData(nameof(VerticalSteps))]
    public void GivenOpenPanes_WhenSteppingVertically_ThenFocusMovesBetweenTheRowsAndStaysAtTheEdges(
        PaneKind current,
        bool down,
        PaneKind expected,
        PaneKind[] open) =>
        Assert.Equal(expected, PaneNavigator.StepVertically(current, down, open));

    public static TheoryData<PaneKind, bool, PaneKind, PaneKind[]> VerticalSteps()
    {
        PaneKind[] all = [PaneKind.Sidebar, PaneKind.Graph, PaneKind.Detail, PaneKind.Diff];
        PaneKind[] lists = [PaneKind.WorkingUnstaged, PaneKind.WorkingStaged];

        return new TheoryData<PaneKind, bool, PaneKind, PaneKind[]>
        {
            { PaneKind.Graph, true, PaneKind.Detail, all },
            { PaneKind.Sidebar, true, PaneKind.Detail, all },
            { PaneKind.Detail, false, PaneKind.Graph, all },
            { PaneKind.Diff, false, PaneKind.Graph, all },
            { PaneKind.Graph, false, PaneKind.Graph, all },
            { PaneKind.Detail, true, PaneKind.Detail, all },
            { PaneKind.Graph, true, PaneKind.Diff, [PaneKind.Graph, PaneKind.Diff] }, // Only the diff below.
            { PaneKind.Graph, true, PaneKind.Graph, SidebarAndGraph }, // No bottom pane.
            { PaneKind.Detail, false, PaneKind.Sidebar, [PaneKind.Sidebar, PaneKind.Detail] }, // No graph.
            { PaneKind.WorkingUnstaged, true, PaneKind.WorkingStaged, lists },
            { PaneKind.WorkingStaged, false, PaneKind.WorkingUnstaged, lists },
            { PaneKind.WorkingUnstaged, false, PaneKind.WorkingUnstaged, lists },
            { PaneKind.WorkingStaged, true, PaneKind.WorkingStaged, lists },
            { PaneKind.WorkingUnstagedDiff, true, PaneKind.WorkingUnstagedDiff, lists },
            { PaneKind.WorkingUnstagedDiff, false, PaneKind.WorkingUnstagedDiff, lists },
            { PaneKind.WorkingStagedDiff, true, PaneKind.WorkingStagedDiff, lists },
            { PaneKind.WorkingStagedDiff, false, PaneKind.WorkingStagedDiff, lists },
        };
    }

}
