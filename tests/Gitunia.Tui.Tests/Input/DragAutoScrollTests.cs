using Gitunia.Tui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

public class DragAutoScrollTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(5, 10)]
    [InlineData(9, 10)]
    public void GivenThePointerInsideThePane_WhenStepping_ThenItIsZero(int pointerRow, int visibleRows)
    {
        Assert.Equal(0, DragAutoScroll.Step(pointerRow, visibleRows));
    }

    [Theory]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 1)]
    [InlineData(12, 10, 2)]
    [InlineData(13, 10, 2)]
    [InlineData(500, 10, DragAutoScroll.MaxStep)]
    public void GivenThePointerBelowThePane_WhenStepping_ThenTheStepGrowsAndIsCapped(int pointerRow, int visibleRows, int expected)
    {
        Assert.Equal(expected, DragAutoScroll.Step(pointerRow, visibleRows));
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(-3, -2)]
    [InlineData(-500, -DragAutoScroll.MaxStep)]
    public void GivenThePointerAboveThePane_WhenStepping_ThenTheStepIsNegative(int pointerRow, int expected)
    {
        Assert.Equal(expected, DragAutoScroll.Step(pointerRow, 10));
    }

    [Fact]
    public void GivenAnEmptyPane_WhenStepping_ThenItIsZero()
    {
        Assert.Equal(0, DragAutoScroll.Step(5, 0));
    }
}
