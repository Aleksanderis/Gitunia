using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Commits;
using Gitunia.Core.Keymap;
using Gitunia.Core.Persistence;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Tests.Fakes;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.WorkingTree;

[Trait("Os", "Independent")]
public class StateMessageBoxPortTests
{
    [Fact]
    public void GivenNoTab_WhenReadingThePort_ThenThereIsNoDraftCursorOrCommit()
    {
        var port = new StateMessageBoxPort(new Dispatcher(AppState.Empty));

        Assert.Null(port.Draft);
        Assert.False(port.ShowsCursor);
        Assert.False(port.IsCommitting);
    }

    [Fact]
    public void GivenAnActiveTabWithADraft_WhenReadingThePort_ThenItShowsTheDraftOfThatTab()
    {
        var shell = new TestShell();
        var tabId = OpenTab(shell);
        shell.Host.Dispatch(new CommitDraftRestored(tabId, new SessionDraft("kept", false, false)));
        var port = new StateMessageBoxPort(shell.Host);

        Assert.Equal("kept", port.Draft!.Editor.Text);
        Assert.False(port.IsCommitting);
    }

    [Fact]
    public void GivenTheBoxInInsertMode_WhenReadingThePort_ThenTheCursorShows()
    {
        var shell = new TestShell();
        var tabId = OpenTab(shell);
        var port = new StateMessageBoxPort(shell.Host);
        Assert.False(port.ShowsCursor);

        shell.Host.Dispatch(new CommitActionPressed(tabId, ActionIds.WorkingTreeWriteMessage));

        Assert.True(port.ShowsCursor);
    }

    [Fact]
    public void GivenARunningCommit_WhenReadingThePort_ThenItIsCommitting()
    {
        var shell = new TestShell();
        var tabId = OpenTab(shell);
        shell.Host.Dispatch(new CommitDraftRestored(tabId, new SessionDraft("message", false, false)));
        var port = new StateMessageBoxPort(shell.Host);

        shell.Host.Dispatch(new CommitActionPressed(tabId, ActionIds.WorkingTreeCommit));

        Assert.True(port.IsCommitting);
    }

    private static TabId OpenTab(TestShell shell)
    {
        var location = new RepositoryLocation("/repo", "/repo", "/repo", false, false);
        shell.Host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = shell.Host.Workspace.ActiveTabId!.Value;
        shell.Host.Dispatch(new TabCommand(tabId, new WorkingTreeLoadedCommand(Snapshot(StagedModified("a.txt")))));
        shell.Host.Dispatch(new TabCommand(tabId, new OpenWorkingTreeCommand()));

        return tabId;
    }
}
