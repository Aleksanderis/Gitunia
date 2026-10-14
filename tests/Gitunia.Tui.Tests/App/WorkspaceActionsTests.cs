using Gitunia.Core.Keymap;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class WorkspaceActionsTests
{
    [Fact]
    public void GivenThreeTabs_WhenATabClickRunsTabNextWithItsNumber_ThenThatTabIsActive()
    {
        var (actions, host, _) = Create(3);

        var handled = actions.TryExecute(ActionIds.TabNext, 3);

        Assert.True(handled);
        Assert.Equal(host.Workspace.Tabs[2].Id, host.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenATabIsClicked_ThenThePickerClosesAndTheTabIsActive()
    {
        var (actions, host, picker) = Create(2);
        picker.Open();

        actions.TryExecute(ActionIds.TabNext, 1);

        Assert.False(picker.IsOpen);
        Assert.Equal(host.Workspace.Tabs[0].Id, host.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenNoTabs_WhenATabClickArrives_ThenNothingHappens()
    {
        var (actions, host, _) = Create(0);

        var handled = actions.TryExecute(ActionIds.TabNext, 1);

        Assert.True(handled);
        Assert.Empty(host.Workspace.Tabs);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenTheMouseDoubleClickActionRuns_ThenThePickerHandlesIt()
    {
        var (actions, _, picker) = Create(0);
        picker.Open();

        var handled = actions.TryExecute(ActionIds.PickerAccept, null);

        Assert.True(handled);
    }

    [Fact]
    public void GivenAnOpenPickerWithAQuery_WhenPickerOpenRunsAgain_ThenTheStateIsKept()
    {
        var (actions, _, picker) = Create(1);
        picker.Open();
        picker.Apply(new TypeTextCommand("abc"));

        actions.TryExecute(ActionIds.PickerOpen, null);

        Assert.Equal("abc", picker.State!.Query);
    }

    private static WorkspaceActionsHarness Create(int tabCount)
    {
        var host = new WorkspaceHost(Workspace.Empty);
        for (var index = 0; index < tabCount; index++)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-workspace-actions", $"repo{index}");
            host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false)));
        }

        // Background results are queued and never run: these tests are synchronous and must not race a scan on another thread.
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var picker = new PickerController(TestGit.Services.Discovery, host, Path.GetTempPath(), pump.Post);
        return new WorkspaceActionsHarness(new WorkspaceActions(host, picker, () => { }), host, picker);
    }
}
