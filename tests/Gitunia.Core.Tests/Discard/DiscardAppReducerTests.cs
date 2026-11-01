using Gitunia.Core.Discard;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Core.Overlays;
using Gitunia.Core.Running;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Stash;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Discard;

// The discard reducer inside the root reducer: running marks, exclusion, the range, and a dialog that outlives its tab.
public class DiscardAppReducerTests
{
    [Fact]
    public void GivenTheDialogAnsweredYes_WhenReducing_ThenTheTabIsBusyUntilTheResultArrives()
    {
        var (state, tabId) = Opened(Modified("a.txt"), Modified("b.txt"));
        var asked = OpenDialog(state, tabId);

        var started = AppReducer.Reduce(asked, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.IsType<DiscardSelectionEffect>(Assert.Single(started.Effects));
        Assert.True(Running(started.State, tabId).IsRunning(RunningOperationKind.Discard));

        var busy = AppReducer.Reduce(started.State, new DiscardRequested(tabId));
        Assert.Equal(DiscardReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(busy.Effects)).Text);

        var done = AppReducer.Reduce(
            started.State,
            new DiscardSucceeded(tabId, "Discarded all changes to a.txt", StatusSeverity.Info, null));
        Assert.False(Running(done.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenTheDialogAnsweredNo_WhenReducing_ThenNothingStarts()
    {
        var (state, tabId) = Opened(Modified("a.txt"));
        var asked = OpenDialog(state, tabId);

        var update = AppReducer.Reduce(asked, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Empty(update.Effects);
        Assert.False(Running(update.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenTheTabIsClosedWhileTheDialogIsOpen_WhenYIsAnswered_ThenNothingIsDiscardedAndTheDialogCloses()
    {
        var (state, tabId) = Opened(Modified("a.txt"));
        state = OpenDialog(state, tabId);
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAClosedTab_WhenADiscardResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened(Modified("a.txt"));

        var update = AppReducer.Reduce(
            state,
            new DiscardSucceeded(new TabId(99), "x", StatusSeverity.Info, null));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAFailure_WhenItArrives_ThenTheTabIsFreeAgain()
    {
        var (state, tabId) = Opened(Modified("a.txt"));
        var started = AppReducer.Reduce(OpenDialog(state, tabId), new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        var failed = AppReducer.Reduce(started.State, new DiscardFailed(tabId, "Could not discard: x"));

        Assert.False(Running(failed.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenAStageRuns_WhenDiscarding_ThenItIsRefusedAsBusyAndTheOtherWayRound()
    {
        var (state, tabId) = Opened(Modified("a.txt"));
        var staging = AppReducer.Reduce(state, new StagingRequested(tabId, StagingAction.Stage)).State;

        var refused = AppReducer.Reduce(staging, new DiscardRequested(tabId));
        Assert.Equal(DiscardReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(refused.Effects)).Text);

        var discarding = AppReducer.Reduce(OpenDialog(state, tabId), new ConfirmAnsweredCommand(ConfirmAnswer.Yes)).State;
        var stageRefused = AppReducer.Reduce(discarding, new StagingRequested(tabId, StagingAction.Stage));
        Assert.Equal(StagingReducer.BlockedNotice, Assert.IsType<PostStatusEffect>(Assert.Single(stageRefused.Effects)).Text);
    }

    [Theory]
    [InlineData(RunningOperationKind.Conflicts)]
    [InlineData(RunningOperationKind.Sync)]
    [InlineData(RunningOperationKind.Branches)]
    [InlineData(RunningOperationKind.Integration)]
    [InlineData(RunningOperationKind.Pick)]
    [InlineData(RunningOperationKind.Rebase)]
    [InlineData(RunningOperationKind.Stash)]
    public void GivenAnOperationThatWritesTheTree_WhenDiscarding_ThenItIsRefused(RunningOperationKind kind)
    {
        var (state, tabId) = Opened(Modified("a.txt"));

        var refused = AppReducer.Reduce(WithRunning(state, tabId, kind), new DiscardRequested(tabId));

        Assert.Equal(DiscardReducer.BlockedNotice, Assert.IsType<PostStatusEffect>(Assert.Single(refused.Effects)).Text);
    }

    [Theory]
    [InlineData(RunningOperationKind.Tags)]
    [InlineData(RunningOperationKind.Remotes)]
    public void GivenAnOperationThatLeavesTheTreeAlone_WhenDiscarding_ThenTheDialogOpens(RunningOperationKind kind)
    {
        var (state, tabId) = Opened(Modified("a.txt"));

        var asked = AppReducer.Reduce(WithRunning(state, tabId, kind), new DiscardRequested(tabId));

        Assert.IsType<ShowConfirmEffect>(Assert.Single(asked.Effects));
    }

    [Fact]
    public void GivenADiscardRuns_WhenAnOperationThatWritesTheTreeIsAsked_ThenItIsRefused()
    {
        var (state, tabId) = Opened(Modified("a.txt"));
        var discarding = AppReducer.Reduce(OpenDialog(state, tabId), new ConfirmAnsweredCommand(ConfirmAnswer.Yes)).State;

        var refused = AppReducer.Reduce(discarding, new StashPushRequested(tabId, false));

        Assert.Equal(StashReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(refused.Effects)).Text);
    }

    [Fact]
    public void GivenAVisualRangeOfFiles_WhenYIsAnswered_ThenTheRangeEndsAndWhenNIsAnsweredItSurvives()
    {
        var (state, tabId) = Opened(Modified("a.txt"), Modified("b.txt"));
        state = AppReducer.Reduce(state, new VisualToggled(0, true)).State;
        state = AppReducer.Reduce(state, new VisualCursorMoved(1)).State;
        var asked = AppReducer.Reduce(state, new DiscardRequested(tabId, state.Shell.Visual!.Range));
        var dialog = Assert.IsType<ShowConfirmEffect>(Assert.Single(asked.Effects));
        var shown = AppReducer.Reduce(asked.State, new ConfirmRequestedCommand(dialog.Prompt, dialog.OnAccepted, null)).State;

        var declined = AppReducer.Reduce(shown, new ConfirmAnsweredCommand(ConfirmAnswer.No));
        Assert.NotNull(declined.State.Shell.Visual);

        var started = AppReducer.Reduce(shown, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        Assert.IsType<DiscardFilesEffect>(Assert.Single(started.Effects));
        Assert.Null(started.State.Shell.Visual);
        Assert.Equal(KeyMode.Normal, started.State.Shell.Mode);
    }

    private static AppState OpenDialog(AppState state, TabId tabId)
    {
        var asked = AppReducer.Reduce(state, new DiscardRequested(tabId));
        var dialog = Assert.IsType<ShowConfirmEffect>(Assert.Single(asked.Effects));

        return AppReducer.Reduce(asked.State, new ConfirmRequestedCommand(dialog.Prompt, dialog.OnAccepted, null)).State;
    }

    private static RunningOperations Running(AppState state, TabId tabId) => state.Workspace.FindTab(tabId)!.Running;

    private static AppState WithRunning(AppState state, TabId tabId, RunningOperationKind kind)
    {
        var tab = state.Workspace.FindTab(tabId)!;
        var running = new RunningOperations([new RunningOperation(1, kind)], 1);
        var tabs = state.Workspace.Tabs.SetItem(0, tab with { Running = running });

        return state with { Workspace = state.Workspace with { Tabs = tabs } };
    }

    private static (AppState State, TabId TabId) Opened(params StatusEntry[] entries)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-discard-app-tests", "alpha");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
        var tabId = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new WorkingTreeLoadedCommand(Snapshot(entries)))).State;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new OpenWorkingTreeCommand())).State;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new FocusPaneCommand(PaneKind.WorkingUnstaged))).State;

        return (state, tabId);
    }
}
