using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

public class MouseControllerTests
{
    private readonly List<ExecutedAction> _executed = [];
    private bool _suppressed;

    [Fact]
    public void GivenAWheelOnTheGraph_WhenHandled_ThenListDownRunsWithTheWheelCount()
    {
        var controller = CreateController();

        var ran = controller.Handle(DefaultKeymap.GraphView, MouseGesture.WheelDown);

        Assert.True(ran);
        Assert.Equal([new ExecutedAction(ActionIds.ListDown, DefaultMouseMap.WheelLines)], _executed);
    }

    [Fact]
    public void GivenAClickOnTheThirdTab_WhenHandled_ThenTabNextRunsWithTheTabNumber()
    {
        var controller = CreateController();

        controller.Handle(DefaultMouseMap.TabsView, MouseGesture.Click, 3);

        Assert.Equal([new ExecutedAction(ActionIds.TabNext, 3)], _executed);
    }

    [Fact]
    public void GivenAClickOnTheDetailPane_WhenHandled_ThenTheFolderToggles()
    {
        var controller = CreateController();

        controller.Handle(DefaultKeymap.DetailView, MouseGesture.Click);

        Assert.Equal([new ExecutedAction(ActionIds.DetailToggleFolder, null)], _executed);
    }

    [Fact]
    public void GivenADoubleClickOnTheDetailPane_WhenHandled_ThenTheDiffOpensLikeEnter()
    {
        var controller = CreateController();

        controller.Handle(DefaultKeymap.DetailView, MouseGesture.DoubleClick);

        Assert.Equal([new ExecutedAction(ActionIds.DetailOpenDiff, null)], _executed);
    }

    [Fact]
    public void GivenADoubleClickOnTheGraph_WhenHandled_ThenTheRowMenuOpens()
    {
        var controller = CreateController();

        controller.Handle(DefaultKeymap.GraphView, MouseGesture.DoubleClick);

        Assert.Equal([new ExecutedAction(ActionIds.MenuOpenForRow, null)], _executed);
    }

    [Fact]
    public void GivenAGestureWithoutABinding_WhenHandled_ThenNothingRuns()
    {
        var controller = CreateController();

        var ran = controller.Handle(DefaultKeymap.GraphView, MouseGesture.Click);

        Assert.False(ran);
        Assert.Empty(_executed);
    }

    [Fact]
    public void GivenAnOverlayIsOpen_WhenHandlingAGesture_ThenNothingRunsBehindIt()
    {
        var controller = CreateController();
        _suppressed = true;

        var ran = controller.Handle(DefaultKeymap.GraphView, MouseGesture.WheelDown);

        Assert.False(ran);
        Assert.Empty(_executed);
    }

    [Fact]
    public void GivenAnOverlayIsOpen_WhenTheWheelTurnsOnAnOverlayView_ThenTheOverlayStillScrolls()
    {
        var controller = CreateController();
        _suppressed = true;

        var ran = controller.Handle(DefaultKeymap.HelpView, MouseGesture.WheelDown);

        Assert.True(ran);
        Assert.Equal([new ExecutedAction(ActionIds.ListDown, DefaultMouseMap.WheelLines)], _executed);
    }

    [Fact]
    public void GivenAnOverlayIsOpen_WhenClickingOutsideIt_ThenItCancelsLikeEsc()
    {
        var controller = CreateController();
        _suppressed = true;

        var ran = controller.Handle(DefaultMouseMap.BackdropView, MouseGesture.Click);

        Assert.True(ran);
        Assert.Equal([new ExecutedAction(ActionIds.Cancel, null)], _executed);
    }

    [Fact]
    public void GivenAPaletteIsOpen_WhenClickingAnEntry_ThenTheEntryRuns()
    {
        var controller = CreateController();
        _suppressed = true;

        controller.Handle(DefaultKeymap.PaletteView, MouseGesture.Click);

        Assert.Equal([new ExecutedAction(ActionIds.CommandExecute, null)], _executed);
    }

    [Theory]
    [InlineData(MouseGesture.DragUp, "list.up")]
    [InlineData(MouseGesture.DragDown, "list.down")]
    public void GivenADragOnTheGraph_WhenTheCursorMustMoveSomeRows_ThenTheMotionRunsWithTheRowDistance(MouseGesture gesture, string action)
    {
        var controller = CreateController();

        controller.Handle(DefaultKeymap.GraphView, gesture, 7);

        Assert.Equal([new ExecutedAction(new ActionId(action), 7)], _executed);
    }

    [Fact]
    public void GivenADragStart_WhenHandled_ThenVisualModeStartsLikeTheVKey()
    {
        var controller = CreateController();

        controller.Handle(DefaultKeymap.DiffView, MouseGesture.DragStart);

        Assert.Equal([new ExecutedAction(ActionIds.VisualToggle, null)], _executed);
    }

    [Fact]
    public void GivenAClickOnTheCommandsButton_WhenHandled_ThenThePaletteOpens()
    {
        var controller = CreateController();

        controller.Handle(DefaultMouseMap.CommandsButton, MouseGesture.Click);

        Assert.Equal([new ExecutedAction(ActionIds.PaletteOpen, null)], _executed);
    }

    [Fact]
    public void GivenAClickOnTheNewTabButton_WhenHandled_ThenThePickerOpens()
    {
        var controller = CreateController();

        controller.Handle(DefaultMouseMap.NewTabButton, MouseGesture.Click);

        Assert.Equal([new ExecutedAction(ActionIds.PickerOpen, null)], _executed);
    }

    [Fact]
    public void GivenAnOverlayIsOpen_WhenClickingTheCommandsButton_ThenNothingRuns()
    {
        var controller = CreateController();
        _suppressed = true;

        Assert.False(controller.Handle(DefaultMouseMap.CommandsButton, MouseGesture.Click));
    }

    [Theory]
    [InlineData(DefaultMouseMap.BannerContinueButton, "operation.continue")]
    [InlineData(DefaultMouseMap.BannerAbortButton, "operation.abort")]
    [InlineData(DefaultMouseMap.BannerSkipButton, "operation.skip")]
    [InlineData(DefaultMouseMap.DiffPreviousFileButton, "diff.previous-file")]
    [InlineData(DefaultMouseMap.DiffNextFileButton, "diff.next-file")]
    [InlineData(DefaultMouseMap.DiffPreviousHunkButton, "diff.previous-hunk")]
    [InlineData(DefaultMouseMap.DiffNextHunkButton, "diff.next-hunk")]
    [InlineData(DefaultMouseMap.DiffWrapButton, "diff.toggle-wrap")]
    [InlineData(DefaultMouseMap.DiffZoomButton, "pane.zoom")]
    [InlineData(DefaultMouseMap.PickerPinButton, "picker.pin")]
    [InlineData(DefaultMouseMap.PickerRemoveButton, "picker.remove-recent")]
    [InlineData(DefaultMouseMap.PickerCloneButton, "picker.clone")]
    [InlineData(DefaultMouseMap.PickerInitButton, "picker.init")]
    public void GivenAButtonClick_WhenHandled_ThenItRunsTheActionOfItsKey(string buttonView, string action)
    {
        var controller = CreateController();

        controller.Handle(buttonView, MouseGesture.Click);

        Assert.Equal([new ExecutedAction(new ActionId(action), null)], _executed);
    }

    private MouseController CreateController() =>
        new(DefaultMouseMap.Create(), (action, count) => _executed.Add(new ExecutedAction(action, count)), () => _suppressed);
}
