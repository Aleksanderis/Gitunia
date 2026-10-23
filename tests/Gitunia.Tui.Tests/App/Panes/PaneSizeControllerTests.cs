using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

[Trait("Os", "Independent")]
public class PaneSizeControllerTests
{
    private PaneArrangement _arrangement = PaneArrangement.Normal;
    private PaneKind _focused = PaneKind.Sidebar;
    private PaneSizes? _applied;

    [Fact]
    public void GivenTheSidebarFocused_WhenWidening_ThenNewSizesAreApplied()
    {
        var controller = CreateController();

        var handled = controller.TryExecute(ActionIds.PaneWider, null);

        Assert.True(handled);
        Assert.Equal(new PaneSizes(SidebarWidth: 32), _applied);
        Assert.Equal(string.Empty, controller.Notice);
    }

    [Fact]
    public void GivenAnActionThatIsNotAResize_WhenExecuting_ThenItIsLeftToOthers()
    {
        var controller = CreateController();

        Assert.False(controller.TryExecute(ActionIds.ListDown, null));
        Assert.Null(_applied);
    }

    [Fact]
    public void GivenAZoomedPane_WhenResizing_ThenNothingChangesAndANoticeSaysWhy()
    {
        _arrangement = new PaneArrangement(false, PaneKind.Graph);
        var controller = CreateController();

        controller.TryExecute(ActionIds.PaneWider, null);

        Assert.Null(_applied);
        Assert.Equal("Unzoom the pane to resize", controller.Notice);
    }

    [Fact]
    public void GivenNothingToResize_WhenResizing_ThenANoticeSaysSo()
    {
        _focused = PaneKind.Detail;
        var controller = CreateController();

        controller.TryExecute(ActionIds.PaneWider, null);

        Assert.Null(_applied);
        Assert.Equal("This pane cannot be resized further", controller.Notice);
    }

    [Fact]
    public void GivenSizesAlreadyAutomatic_WhenResetting_ThenANoticeSaysSo()
    {
        var controller = CreateController();

        controller.TryExecute(ActionIds.PaneResetSizes, null);

        Assert.Null(_applied);
        Assert.Equal("Pane sizes are already automatic", controller.Notice);
    }

    [Fact]
    public void GivenChangedSizes_WhenResetting_ThenAutomaticSizesAreApplied()
    {
        _arrangement = new PaneArrangement(false, null, new PaneSizes(SidebarWidth: 40));
        var controller = CreateController();

        controller.TryExecute(ActionIds.PaneResetSizes, null);

        Assert.Equal(PaneSizes.Automatic, _applied);
    }

    private PaneSizeController CreateController() =>
        new(() => _arrangement, () => _focused, () => new ScreenSize(120, 40), sizes => _applied = sizes);
}
