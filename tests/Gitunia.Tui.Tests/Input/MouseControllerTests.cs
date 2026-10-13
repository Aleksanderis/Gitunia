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

    [Fact]
    public void GivenAnOverlayIsOpen_WhenTheWheelTurnsOnAnOverlayView_ThenTheOverlayStillScrolls()
    {
        var controller = CreateController();
        suppressed = true;

        var ran = controller.Handle(DefaultKeymap.HelpView, MouseGesture.WheelDown);

        Assert.True(ran);
        Assert.Equal([(ActionIds.ListDown, (int?)DefaultMouseMap.WheelLines)], executed);
    }

    [Fact]
    public void GivenAnOverlayIsOpen_WhenClickingOutsideIt_ThenItCancelsLikeEsc()
    {
        var controller = CreateController();
        suppressed = true;

        var ran = controller.Handle(DefaultMouseMap.BackdropView, MouseGesture.Click);

        Assert.True(ran);
        Assert.Equal([(ActionIds.Cancel, (int?)null)], executed);
    }

    [Fact]
    public void GivenAPaletteIsOpen_WhenClickingAnEntry_ThenTheEntryRuns()
    {
        var controller = CreateController();
        suppressed = true;

        controller.Handle(DefaultKeymap.PaletteView, MouseGesture.Click);

        Assert.Equal([(ActionIds.CommandExecute, (int?)null)], executed);
    }

    [Theory]
    [InlineData(MouseGesture.DragUp, "list.up")]
    [InlineData(MouseGesture.DragDown, "list.down")]
    public void GivenADragOnTheGraph_WhenTheCursorMustMoveSomeRows_ThenTheMotionRunsWithTheRowDistance(MouseGesture gesture, string action)
    {
        var controller = CreateController();

        controller.Handle(DefaultKeymap.GraphView, gesture, 7);

        Assert.Equal([(new ActionId(action), (int?)7)], executed);
    }

    [Fact]
    public void GivenADragStart_WhenHandled_ThenVisualModeStartsLikeTheVKey()
    {
        var controller = CreateController();

        controller.Handle(DefaultKeymap.DiffView, MouseGesture.DragStart);

        Assert.Equal([(ActionIds.VisualToggle, (int?)null)], executed);
    }

    [Fact]
    public void GivenAClickOnTheHeader_WhenHandled_ThenThePaletteOpens()
    {
        var controller = CreateController();

        controller.Handle(DefaultMouseMap.HeaderView, MouseGesture.Click);

        Assert.Equal([(ActionIds.PaletteOpen, (int?)null)], executed);
    }

    [Fact]
    public void GivenAnOverlayIsOpen_WhenClickingTheHeader_ThenNothingRuns()
    {
        var controller = CreateController();
        suppressed = true;

        Assert.False(controller.Handle(DefaultMouseMap.HeaderView, MouseGesture.Click));
    }

    [Theory]
    [InlineData(DefaultMouseMap.DiffPreviousFileButton, "diff.previous-file")]
    [InlineData(DefaultMouseMap.DiffNextFileButton, "diff.next-file")]
    [InlineData(DefaultMouseMap.DiffPreviousHunkButton, "diff.previous-hunk")]
    [InlineData(DefaultMouseMap.DiffNextHunkButton, "diff.next-hunk")]
    [InlineData(DefaultMouseMap.DiffWrapButton, "diff.toggle-wrap")]
    [InlineData(DefaultMouseMap.DiffZoomButton, "pane.zoom")]
    [InlineData(DefaultMouseMap.DiffCloseButton, "navigate.back")]
    [InlineData(DefaultMouseMap.PickerPinButton, "picker.pin")]
    [InlineData(DefaultMouseMap.PickerRemoveButton, "picker.remove-recent")]
    public void GivenAButtonClick_WhenHandled_ThenItRunsTheActionOfItsKey(string buttonView, string action)
    {
        var controller = CreateController();

        controller.Handle(buttonView, MouseGesture.Click);

        Assert.Equal([(new ActionId(action), (int?)null)], executed);
    }

    private MouseController CreateController() =>
        new(DefaultMouseMap.Create(), (action, count) => executed.Add((action, count)), () => suppressed);
}
