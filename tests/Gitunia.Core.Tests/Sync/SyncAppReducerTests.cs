using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Remotes;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Sync;

// The sync reducer inside the root reducer: running marks, the tab's progress line, dropped late commands and the
// open force push dialog.
public class SyncAppReducerTests
{
    private static readonly ConfirmPrompt ForcePrompt =
        new("Force push", ["Force push topic to origin/topic?"], "Undo: git push ...", "Force push");

    private static readonly PushRequest ForceRequest = new("origin", "topic", false, new PushLease("topic", "abc"));

    [Fact]
    public void GivenAFetch_WhenReducing_ThenTheTabIsBusyWithAProgressLineUntilTheResultArrives()
    {
        var (state, tabId) = Opened();

        var started = AppReducer.Reduce(state, new SyncRequested(tabId, SyncKind.Fetch));
        Assert.IsType<FetchEffect>(Assert.Single(started.Effects));
        var tab = started.State.Workspace.FindTab(tabId)!;
        Assert.True(tab.Running.IsRunning(RunningOperationKind.Sync));
        Assert.Equal("Fetching...", tab.Sync!.ProgressLine);

        var progressed = AppReducer.Reduce(
            started.State,
            new SyncProgressed(tabId, new RemoteProgress("Receiving objects", 45)));
        Assert.Equal(
            "Fetching: Receiving objects 45%",
            progressed.State.Workspace.FindTab(tabId)!.Sync!.ProgressLine);

        var done = AppReducer.Reduce(progressed.State, new SyncSucceeded(tabId, "Fetched all remotes"));
        var finished = done.State.Workspace.FindTab(tabId)!;
        Assert.False(finished.Running.IsBusy);
        Assert.Null(finished.Sync);
    }

    [Fact]
    public void GivenAFetchRuns_WhenFetchingAgain_ThenItIsRefusedAndTheFirstKeepsRunning()
    {
        var (state, tabId) = Opened();
        var started = AppReducer.Reduce(state, new SyncRequested(tabId, SyncKind.Fetch));

        var again = AppReducer.Reduce(started.State, new SyncRequested(tabId, SyncKind.Fetch));

        Assert.IsType<PostStatusEffect>(Assert.Single(again.Effects));
        Assert.True(again.State.Workspace.FindTab(tabId)!.Running.IsRunning(RunningOperationKind.Sync));
    }

    [Fact]
    public void GivenARemoteChangeRuns_WhenPulling_ThenItIsRefusedAsBusy()
    {
        var (state, tabId) = Opened();
        state = AppReducer.Reduce(state, new RemoteActionRequested(tabId, RemoteAction.Add, new RemoteTarget(null, null)))
            .State;

        var update = AppReducer.Reduce(state, new SyncRequested(tabId, SyncKind.Pull));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(SyncReducer.OtherBusyNotice, post.Text);
    }

    [Fact]
    public void GivenAClosedTab_WhenASyncResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened();

        var update = AppReducer.Reduce(state, new SyncSucceeded(new TabId(99), "Fetched all remotes"));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenForcePushDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingIsPushedAndTheDialogCloses()
    {
        var (state, tabId) = Opened();
        var asked = AppReducer.Reduce(
            state,
            new ConfirmRequestedCommand(ForcePrompt, new SyncForceConfirmed(tabId, ForceRequest, "Force pushed"), null));
        state = AppReducer.Reduce(asked.State, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenForcePushDialog_WhenYIsAnswered_ThenTheLeasePushStartsAndTheTabIsBusy()
    {
        var (state, tabId) = Opened();
        var asked = AppReducer.Reduce(
            state,
            new ConfirmRequestedCommand(ForcePrompt, new SyncForceConfirmed(tabId, ForceRequest, "Force pushed"), null));

        var update = AppReducer.Reduce(asked.State, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        var push = Assert.IsType<PushEffect>(Assert.Single(update.Effects));
        Assert.Equal(ForceRequest, push.Request);
        var tab = update.State.Workspace.FindTab(tabId)!;
        Assert.True(tab.Running.IsRunning(RunningOperationKind.Sync));
        Assert.Equal("Pushing...", tab.Sync!.ProgressLine);
    }

    [Fact]
    public void GivenAnOpenForcePushDialog_WhenNIsAnswered_ThenNothingStarts()
    {
        var (state, tabId) = Opened();
        var asked = AppReducer.Reduce(
            state,
            new ConfirmRequestedCommand(ForcePrompt, new SyncForceConfirmed(tabId, ForceRequest, "Force pushed"), null));

        var update = AppReducer.Reduce(asked.State, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Empty(update.Effects);
        Assert.False(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenAnOpenDeleteDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingIsDeleted()
    {
        var (state, tabId) = Opened();
        var prompt = new ConfirmPrompt("Delete branch on remote", ["Delete?"], "Undo: git push", "Delete on remote");
        var asked = AppReducer.Reduce(
            state,
            new ConfirmRequestedCommand(prompt, new SyncDeleteConfirmed(tabId, "origin", "topic", "abc"), null));
        state = AppReducer.Reduce(asked.State, new CloseTabCommand(tabId)).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenDialog_WhenAPushIsRejected_ThenNoForceConfirmIsOfferedOverIt()
    {
        var (state, tabId) = Opened();
        var asked = AppReducer.Reduce(state, new ConfirmRequestedCommand(ForcePrompt, new SyncCancelRequested(tabId), null));

        var update = AppReducer.Reduce(
            asked.State,
            new SyncFailed(tabId, "Push failed", null, new SyncRejection("topic", null)));

        Assert.Equal(2, update.Effects.Length);
    }

    private static (AppState State, TabId TabId) Opened()
    {
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(Location())).State;

        return (state, state.Workspace.Tabs[0].Id);
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-sync-app-tests", "alpha");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
