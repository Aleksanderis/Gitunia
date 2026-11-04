using Gitunia.Tui.Rendering.Tabs;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Tabs;

[Trait("Os", "Independent")]
public class TabDragPlannerTests
{
    // Chips: 1 at 1..11, 2 at 13..22, 3 at 24..34 (one blank column between them).
    private static readonly TabCell[] Cells =
    [
        new(1, 1, "  1 alpha  ", false),
        new(2, 13, "  2 beta  ", true),
        new(3, 24, "  3 gamma  ", false),
    ];

    [Fact]
    public void GivenThePointerInsideTheDraggedChip_WhenPlanning_ThenItStays()
    {
        Assert.Equal(0, TabDragPlanner.Step(Cells, 2, 14));
        Assert.Equal(0, TabDragPlanner.Step(Cells, 2, 22));
    }

    [Fact]
    public void GivenThePointerLeftOfTheChipOnTheNeighbour_WhenPlanning_ThenItMovesLeft()
    {
        Assert.Equal(-1, TabDragPlanner.Step(Cells, 2, 5));
    }

    [Fact]
    public void GivenThePointerRightOfTheChipOnTheNeighbour_WhenPlanning_ThenItMovesRight()
    {
        Assert.Equal(1, TabDragPlanner.Step(Cells, 2, 30));
    }

    [Fact]
    public void GivenTheGapBeforeTheNeighbour_WhenPlanning_ThenItStaysUntilTheNeighbourIsCrossed()
    {
        Assert.Equal(0, TabDragPlanner.Step(Cells, 2, 12));
        Assert.Equal(0, TabDragPlanner.Step(Cells, 2, 23));
    }

    [Fact]
    public void GivenTheFirstAndLastChip_WhenThePointerGoesPastTheEnds_ThenItStays()
    {
        Assert.Equal(0, TabDragPlanner.Step(Cells, 1, 0));
        Assert.Equal(0, TabDragPlanner.Step(Cells, 3, 60));
    }

    [Fact]
    public void GivenAChipThatIsNotVisible_WhenPlanning_ThenItStays()
    {
        Assert.Equal(0, TabDragPlanner.Step(Cells, 9, 5));
    }
}
