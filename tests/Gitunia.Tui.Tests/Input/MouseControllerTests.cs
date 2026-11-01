using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Gitunia.Tui.App;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class MouseControllerTests
{
    [Theory]
    [InlineData(DefaultKeymap.DetailChangesView, "detail.toggle-folder", "detail.open-diff")]
    [InlineData(DefaultKeymap.WorkingUnstagedView, "working-tree.toggle-folder", "working-tree.stage")]
    [InlineData(DefaultKeymap.WorkingStagedView, "working-tree.toggle-folder", "working-tree.unstage")]
    public void GivenAFoldingList_WhenDoubleClicking_ThenTheFirstClicksToggleIsUndoneBeforeTheDoubleClickAction(
        string view,
        string toggle,
        string doubleClick)
    {
        var executed = new List<string>();
        var controller = CreateController(executed);

        controller.Handle(view, MouseGesture.DoubleClick);

        Assert.Equal([toggle, doubleClick], executed);
    }

    [Fact]
    public void GivenAFoldingList_WhenClickingOnce_ThenTheFolderIsToggledOnce()
    {
        var executed = new List<string>();
        var controller = CreateController(executed);

        controller.Handle(DefaultKeymap.DetailChangesView, MouseGesture.Click);

        Assert.Equal(["detail.toggle-folder"], executed);
    }

    [Fact]
    public void GivenAListWithoutFolders_WhenDoubleClicking_ThenOnlyTheDoubleClickActionRuns()
    {
        var executed = new List<string>();
        var controller = CreateController(executed);

        controller.Handle(DefaultKeymap.GraphView, MouseGesture.DoubleClick);

        Assert.Equal([ActionIds.MenuOpenForRow.Value], executed);
    }

    private static MouseController CreateController(List<string> executed) =>
        new(DefaultMouseMap.Create(), new RecordingRoute(executed), new MouseGate());

    private sealed class RecordingRoute(List<string> executed) : IActionRoute
    {
        public void Route(ActionId action, int? count) => executed.Add(action.Value);
    }
}
