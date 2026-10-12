using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
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

    private static (WorkspaceActions Actions, WorkspaceHost Host, PickerController Picker) Create(int tabCount)
    {
        var host = new WorkspaceHost(Workspace.Empty);
        for (var index = 0; index < tabCount; index++)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-workspace-actions", "repo" + index);
            host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false)));
        }

        var picker = new PickerController(new GitRunner(), host, Path.GetTempPath(), action => action());
        return (new WorkspaceActions(host, picker, () => { }), host, picker);
    }
}
