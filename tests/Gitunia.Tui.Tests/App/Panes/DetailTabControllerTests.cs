using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Panes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

public class DetailTabControllerTests
{
    [Theory]
    [InlineData(BottomTab.Details, "detail.next-tab", BottomTab.Changes)]
    [InlineData(BottomTab.Changes, "detail.next-tab", BottomTab.Files)]
    [InlineData(BottomTab.Files, "detail.next-tab", BottomTab.Details)]
    [InlineData(BottomTab.Details, "detail.previous-tab", BottomTab.Files)]
    [InlineData(BottomTab.Files, "detail.previous-tab", BottomTab.Changes)]
    [InlineData(BottomTab.Changes, "detail.previous-tab", BottomTab.Details)]
    [InlineData(BottomTab.Changes, "detail.tab-details", BottomTab.Details)]
    [InlineData(BottomTab.Details, "detail.tab-changes", BottomTab.Changes)]
    [InlineData(BottomTab.Details, "detail.tab-files", BottomTab.Files)]
    [InlineData(BottomTab.Files, "detail.tab-files", BottomTab.Files)]
    public void GivenATabAction_WhenExecuting_ThenTheActiveTabsBottomTabChanges(BottomTab start, string action, BottomTab expected)
    {
        var host = HostWithTab(start);
        var controller = new DetailTabController(host);

        var handled = controller.TryExecute(new ActionId(action));

        Assert.True(handled);
        Assert.Equal(expected, host.Workspace.ActiveTab!.View.BottomTab);
    }

    [Fact]
    public void GivenAnotherAction_WhenExecuting_ThenItIsNotHandledAndNothingChanges()
    {
        var host = HostWithTab(BottomTab.Changes);
        var changes = 0;
        host.Changed += () => changes++;

        var handled = new DetailTabController(host).TryExecute(ActionIds.ListDown);

        Assert.False(handled);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void GivenTheTabAlreadyShowing_WhenSelectingItAgain_ThenNothingIsDispatched()
    {
        var host = HostWithTab(BottomTab.Files);
        var changes = 0;
        host.Changed += () => changes++;

        new DetailTabController(host).TryExecute(ActionIds.DetailTabFiles);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void GivenNoOpenTab_WhenExecutingATabAction_ThenItIsHandledAsANoOp()
    {
        var controller = new DetailTabController(new WorkspaceHost(Workspace.Empty));

        Assert.True(controller.TryExecute(ActionIds.DetailNextTab));
    }

    [Fact]
    public void GivenTwoTabs_WhenSwitchingOne_ThenTheOtherKeepsItsBottomTab()
    {
        var host = HostWithTab(BottomTab.Changes);
        host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(Path.Combine(Path.GetTempPath(), "second"), Path.Combine(Path.GetTempPath(), "second", ".git"), Path.Combine(Path.GetTempPath(), "second", ".git"), false, false)));
        var first = host.Workspace.Tabs[0];

        new DetailTabController(host).TryExecute(ActionIds.DetailTabDetails);

        Assert.Equal(BottomTab.Details, host.Workspace.ActiveTab!.View.BottomTab);
        Assert.Equal(first.View.BottomTab, host.Workspace.Tabs[0].View.BottomTab);
    }

    private static WorkspaceHost HostWithTab(BottomTab tab)
    {
        var host = new WorkspaceHost(Workspace.Empty);
        var root = Path.Combine(Path.GetTempPath(), "gitunia-detail-tab-tests");
        var gitDirectory = Path.Combine(root, ".git");
        host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, gitDirectory, gitDirectory, false, false)));
        host.Dispatch(new TabCommand(host.Workspace.ActiveTabId!.Value, new SetBottomTabCommand(tab)));
        return host;
    }
}
