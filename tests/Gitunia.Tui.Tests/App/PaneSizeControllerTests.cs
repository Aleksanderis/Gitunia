using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class PaneSizeControllerTests
{
    private PaneArrangement arrangement = PaneArrangement.Normal;
    private PaneKind focused = PaneKind.Sidebar;
    private PaneSizes? applied;

    [Fact]
    public void GivenTheSidebarFocused_WhenWidening_ThenNewSizesAreApplied()
    {
        var controller = CreateController();

        var handled = controller.TryExecute(ActionIds.PaneWider, null);

        Assert.True(handled);
        Assert.Equal(new PaneSizes(SidebarWidth: 32), applied);
        Assert.Equal(string.Empty, controller.Notice);
    }

    [Fact]
    public void GivenAnActionThatIsNotAResize_WhenExecuting_ThenItIsLeftToOthers()
    {
        var controller = CreateController();

        Assert.False(controller.TryExecute(ActionIds.ListDown, null));
        Assert.Null(applied);
    }

    [Fact]
    public void GivenAZoomedPane_WhenResizing_ThenNothingChangesAndANoticeSaysWhy()
    {
        arrangement = new PaneArrangement(false, PaneKind.Graph);
        var controller = CreateController();

        controller.TryExecute(ActionIds.PaneWider, null);

        Assert.Null(applied);
        Assert.Equal("Unzoom the pane to resize", controller.Notice);
    }

    [Fact]
    public void GivenNothingToResize_WhenResizing_ThenANoticeSaysSo()
    {
        focused = PaneKind.Detail;
        var controller = CreateController();

        controller.TryExecute(ActionIds.PaneWider, null);

        Assert.Null(applied);
        Assert.Equal("This pane cannot be resized further", controller.Notice);
    }

    [Fact]
    public void GivenSizesAlreadyAutomatic_WhenResetting_ThenANoticeSaysSo()
    {
        var controller = CreateController();

        controller.TryExecute(ActionIds.PaneResetSizes, null);

        Assert.Null(applied);
        Assert.Equal("Pane sizes are already automatic", controller.Notice);
    }

    [Fact]
    public void GivenChangedSizes_WhenResetting_ThenAutomaticSizesAreApplied()
    {
        arrangement = new PaneArrangement(false, null, new PaneSizes(SidebarWidth: 40));
        var controller = CreateController();

        controller.TryExecute(ActionIds.PaneResetSizes, null);

        Assert.Equal(PaneSizes.Automatic, applied);
    }

    private PaneSizeController CreateController() =>
        new(() => arrangement, () => focused, () => (120, 40), sizes => applied = sizes);
}
