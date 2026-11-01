using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Panes;

public class PaneSizeReducerTests
{
    private static readonly PaneLayoutFacts Normal = new(false, null, false, false);

    [Fact]
    public void GivenTheSidebarFocused_WhenWidening_ThenTheNewSizeIsInTheTabsViewState()
    {
        var state = WithTab(PaneKind.Sidebar);

        var update = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneWider, null, Normal));

        Assert.Equal(32, update.State.Workspace.ActiveTab!.View.SidebarWidth);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenACount_WhenWidening_ThenTheStepIsMultiplied()
    {
        var state = WithTab(PaneKind.Sidebar);

        var update = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneWider, 3, Normal));

        Assert.Equal(30 + (3 * PaneResizeRules.ColumnStep), update.State.Workspace.ActiveTab!.View.SidebarWidth);
    }

    [Fact]
    public void GivenAZoomedPane_WhenResizing_ThenNothingChangesAndANoticeSaysWhy()
    {
        var state = WithTab(PaneKind.Graph);

        var update = PaneSizeReducer.Reduce(
            state,
            Command(state, ActionIds.PaneWider, null, new PaneLayoutFacts(false, PaneKind.Graph, false, false)));

        Assert.Same(state, update.State);
        Assert.Equal(PaneSizeReducer.ZoomedNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenNothingToResize_WhenResizing_ThenANoticeSaysSo()
    {
        var state = WithTab(PaneKind.Detail);

        var update = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneWider, null, Normal));

        Assert.Same(state, update.State);
        Assert.Equal(PaneSizeReducer.AtLimitNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenSizesAlreadyAutomatic_WhenResetting_ThenANoticeSaysSo()
    {
        var state = WithTab(PaneKind.Sidebar);

        var update = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneResetSizes, null, Normal));

        Assert.Equal(PaneSizeReducer.AlreadyAutomaticNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenChangedSizes_WhenResetting_ThenAutomaticSizesAreStored()
    {
        var state = WithTab(PaneKind.Sidebar);
        state = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneWider, null, Normal)).State;

        var update = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneResetSizes, null, Normal));

        Assert.Null(update.State.Workspace.ActiveTab!.View.SidebarWidth);
        Assert.Empty(update.Effects);
    }

    [Theory]
    [InlineData(PaneKind.Detail)]
    [InlineData(PaneKind.Diff)]
    public void GivenThePairedZoom_WhenWidening_ThenTheDividerBetweenThePanesMoves(PaneKind focused)
    {
        var state = WithTab(focused);
        var layout = new PaneLayoutFacts(true, focused, true, false);

        var update = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneWider, null, layout));

        var before = PaneSizing.DetailWidth(120, true, null);
        var expected = before + (PaneResizeRules.ColumnStep * (focused == PaneKind.Detail ? 1 : -1));
        var view = update.State.Workspace.ActiveTab!.View;
        Assert.Equal(expected, PaneSizing.DetailWidth(120, true, new PaneSizes(null, null, view.DetailWidth)));
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenThePairedZoom_WhenChangingHeight_ThenNothingChangesAndANoticeSaysWhy()
    {
        var state = WithTab(PaneKind.Diff);
        var layout = new PaneLayoutFacts(true, PaneKind.Diff, true, false);

        var update = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneTaller, null, layout));

        Assert.Same(state, update.State);
        Assert.Equal(PaneSizeReducer.ZoomedNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenAZoomedWorkingTreeDiff_WhenResizing_ThenNothingChangesAndANoticeSaysWhy()
    {
        var state = WithTab(PaneKind.Sidebar);

        var update = PaneSizeReducer.Reduce(
            state,
            Command(state, ActionIds.PaneWider, null, new PaneLayoutFacts(false, null, false, true)));

        Assert.Same(state, update.State);
        Assert.Equal(PaneSizeReducer.ZoomedNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenNoKnownScreenSize_WhenResizing_ThenNothingChanges()
    {
        var state = WithTab(PaneKind.Sidebar) with { Shell = ShellState.Empty };

        var update = PaneSizeReducer.Reduce(state, Command(state, ActionIds.PaneWider, null, Normal));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAClosedTab_WhenResizing_ThenNothingChanges()
    {
        var state = WithTab(PaneKind.Sidebar);

        var update = PaneSizeReducer.Reduce(state, new PaneSizeCommand(new TabId(99), ActionIds.PaneWider, null, Normal));

        Assert.Same(state, update.State);
    }

    private static PaneSizeCommand Command(AppState state, ActionId action, int? count, PaneLayoutFacts layout) =>
        new(state.Workspace.ActiveTabId!.Value, action, count, layout);

    private static string NoticeOf(AppUpdate update) =>
        Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text;

    private static AppState WithTab(PaneKind focused)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-pane-size-reducer");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
        var tab = state.Workspace.ActiveTabId!.Value;
        state = AppReducer.Reduce(state, new TabCommand(tab, new FocusPaneCommand(focused))).State;

        return AppReducer.Reduce(state, new ScreenResizedCommand(new ScreenExtent(120, 40), DateTimeOffset.UnixEpoch)).State;
    }
}
