using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Panes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

public class DetailTabInputTests
{
    [Theory]
    [InlineData(BottomTab.Details, "detail.next-tab", BottomTab.Changes)]
    [InlineData(BottomTab.Files, "detail.next-tab", BottomTab.Details)]
    [InlineData(BottomTab.Details, "detail.previous-tab", BottomTab.Files)]
    [InlineData(BottomTab.Changes, "detail.tab-details", BottomTab.Details)]
    [InlineData(BottomTab.Details, "detail.tab-changes", BottomTab.Changes)]
    [InlineData(BottomTab.Details, "detail.tab-files", BottomTab.Files)]
    [InlineData(BottomTab.Files, "detail.tab-files", BottomTab.Files)]
    public void GivenATabAction_WhenExecuting_ThenTheActiveTabsBottomTabChanges(
        BottomTab start,
        string action,
        BottomTab expected)
    {
        var host = HostWithTab();
        host.Dispatch(new TabCommand(host.Workspace.ActiveTabId!.Value, new SetBottomTabCommand(start)));

        var handled = new DetailTabInput(host).TryExecute(new ActionId(action));

        Assert.True(handled);
        Assert.Equal(expected, host.Workspace.ActiveTab!.View.BottomTab);
    }

    [Fact]
    public void GivenAnotherAction_WhenExecuting_ThenItIsNotHandledAndNothingChanges()
    {
        var host = HostWithTab();
        var changes = 0;
        host.Changed += () => changes++;

        var handled = new DetailTabInput(host).TryExecute(ActionIds.ListDown);

        Assert.False(handled);
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData("files.toggle-path-style")]
    [InlineData("files.path-tree")]
    [InlineData("detail.next-tab")]
    public void GivenNoOpenTab_WhenExecutingAnActionOfTheirs_ThenItIsHandledAsANoOp(string action)
    {
        var host = new Dispatcher(AppState.Empty);

        Assert.True(new DetailTabInput(host).TryExecute(new ActionId(action)));
        Assert.Empty(host.Workspace.Tabs);
    }

    [Fact]
    public void GivenTheToggleAction_WhenExecuting_ThenTheActiveTabsPathStyleSteps()
    {
        var host = HostWithTab();
        var input = new DetailTabInput(host);

        input.TryExecute(ActionIds.FilesTogglePathStyle);

        Assert.Equal(FilePathStyle.FullPath, host.Workspace.ActiveTab!.View.PathStyle);
    }

    [Theory]
    [InlineData("files.path-name-first", FilePathStyle.NameFirst)]
    [InlineData("files.path-full-path", FilePathStyle.FullPath)]
    [InlineData("files.path-tree", FilePathStyle.Tree)]
    public void GivenAStyleAction_WhenExecuting_ThenThatStyleIsSetWhateverTheCurrentOne(
        string action,
        FilePathStyle expected)
    {
        var host = HostWithTab();
        var input = new DetailTabInput(host);
        input.TryExecute(ActionIds.FilesTogglePathStyle);

        Assert.True(input.TryExecute(new ActionId(action)));

        Assert.Equal(expected, host.Workspace.ActiveTab!.View.PathStyle);
    }

    private static Dispatcher HostWithTab()
    {
        var host = new Dispatcher(AppState.Empty);
        var root = Path.Combine(Path.GetTempPath(), "gitunia-detail-tab-tests");
        var gitDirectory = Path.Combine(root, ".git");
        host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, gitDirectory, gitDirectory, false, false)));

        return host;
    }
}
