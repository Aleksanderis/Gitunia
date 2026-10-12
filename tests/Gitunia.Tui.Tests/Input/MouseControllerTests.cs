using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

public class MouseControllerTests
{
    private readonly List<(ActionId Action, int? Count)> executed = [];
    private bool suppressed;

    [Fact]
    public void GivenAWheelOnTheGraph_WhenHandled_ThenListDownRunsWithTheWheelCount()
    {
        var controller = CreateController();

        var ran = controller.Handle(DefaultKeymap.GraphView, MouseGesture.WheelDown);

        Assert.True(ran);
        Assert.Equal([(ActionIds.ListDown, (int?)DefaultMouseMap.WheelLines)], executed);
    }

    [Fact]
    public void GivenAClickOnTheThirdTab_WhenHandled_ThenTabNextRunsWithTheTabNumber()
    {
        var controller = CreateController();

        controller.Handle(DefaultMouseMap.TabsView, MouseGesture.Click, 3);

        Assert.Equal([(ActionIds.TabNext, (int?)3)], executed);
    }

    [Fact]
    public void GivenADoubleClickOnTheDetailPane_WhenHandled_ThenTheDiffOpensLikeEnter()
    {
        var controller = CreateController();

        controller.Handle(DefaultKeymap.DetailView, MouseGesture.DoubleClick);

        Assert.Equal([(ActionIds.DetailOpenDiff, (int?)null)], executed);
    }

    [Fact]
    public void GivenAGestureWithoutABinding_WhenHandled_ThenNothingRuns()
    {
        var controller = CreateController();

        var ran = controller.Handle(DefaultKeymap.GraphView, MouseGesture.Click);

        Assert.False(ran);
        Assert.Empty(executed);
    }

    [Fact]
    public void GivenAnOverlayIsOpen_WhenHandlingAGesture_ThenNothingRunsBehindIt()
    {
        var controller = CreateController();
        suppressed = true;

        var ran = controller.Handle(DefaultKeymap.GraphView, MouseGesture.WheelDown);

        Assert.False(ran);
        Assert.Empty(executed);
    }

    private MouseController CreateController() =>
        new(DefaultMouseMap.Create(), (action, count) => executed.Add((action, count)), () => suppressed);
}
