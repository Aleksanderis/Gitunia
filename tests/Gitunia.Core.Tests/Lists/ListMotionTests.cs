using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Xunit;

namespace Gitunia.Core.Tests.Lists;

public class ListMotionTests
{
    private static int Move(ActionId action, int? count, int selected, int itemCount = 100, int pageHeight = 20)
    {
        Assert.True(ListMotion.TryMove(action, count, selected, itemCount, pageHeight, out var target));
        return target;
    }

    [Fact]
    public void GivenDownAndUp_WhenMoving_ThenOneStepOrCountSteps()
    {
        Assert.Equal(6, Move(ActionIds.ListDown, null, 5));
        Assert.Equal(10, Move(ActionIds.ListDown, 5, 5));
        Assert.Equal(4, Move(ActionIds.ListUp, null, 5));
        Assert.Equal(0, Move(ActionIds.ListUp, 9, 5));
    }

    [Fact]
    public void GivenEndsOfList_WhenMoving_ThenSelectionIsClamped()
    {
        Assert.Equal(99, Move(ActionIds.ListDown, 500, 50));
        Assert.Equal(0, Move(ActionIds.ListUp, 1, 0));
    }

    [Fact]
    public void GivenHalfPage_WhenMoving_ThenHalfTheVisibleRowsPerCount()
    {
        Assert.Equal(30, Move(ActionIds.ListHalfPageDown, null, 20));
        Assert.Equal(10, Move(ActionIds.ListHalfPageUp, null, 20));
        Assert.Equal(50, Move(ActionIds.ListHalfPageDown, 3, 20));
    }

    [Fact]
    public void GivenTinyViewport_WhenHalfPaging_ThenItStillMovesOneRow()
    {
        Assert.Equal(6, Move(ActionIds.ListHalfPageDown, null, 5, pageHeight: 1));
    }

    [Fact]
    public void GivenTopAndBottom_WhenMoving_ThenFirstLastOrTheCountedLine()
    {
        Assert.Equal(0, Move(ActionIds.ListTop, null, 50));
        Assert.Equal(99, Move(ActionIds.ListBottom, null, 50));
        Assert.Equal(11, Move(ActionIds.ListBottom, 12, 50));
        Assert.Equal(4, Move(ActionIds.ListTop, 5, 50));
        Assert.Equal(99, Move(ActionIds.ListBottom, 1000, 50));
    }

    [Fact]
    public void GivenEmptyList_WhenMoving_ThenSelectionStaysAtZero()
    {
        Assert.Equal(0, Move(ActionIds.ListDown, 3, 0, itemCount: 0));
        Assert.Equal(0, Move(ActionIds.ListBottom, null, 0, itemCount: 0));
    }

    [Fact]
    public void GivenActionThatIsNotAMotion_WhenMoving_ThenItIsNotHandled()
    {
        Assert.False(ListMotion.TryMove(ActionIds.GraphJumpToHead, null, 3, 10, 5, out var target));
        Assert.Equal(3, target);
    }
}
