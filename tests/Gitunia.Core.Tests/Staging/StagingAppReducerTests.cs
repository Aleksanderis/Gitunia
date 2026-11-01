using Gitunia.Core.Overlays;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Staging;
using Gitunia.Core.Stash;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Staging;

// The staging reducer inside the root reducer: running marks, exclusion, dropped late commands and the open dialog.
public class StagingAppReducerTests
{
    [Fact]
    public void GivenAKey_WhenReducing_ThenTheTabIsBusyUntilTheResultArrives()
    {
        var (state, tabId) = Opened(Modified("a.txt"), Modified("b.txt"));

        var started = AppReducer.Reduce(state, new StagingRequested(tabId, StagingAction.Stage));
        Assert.IsType<StagePathsEffect>(Assert.Single(started.Effects));
        Assert.True(Running(started.State, tabId).IsRunning(RunningOperationKind.Staging));

        var busy = AppReducer.Reduce(started.State, new StagingRequested(tabId, StagingAction.Stage));
        Assert.Equal(StagingReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(busy.Effects)).Text);

        var done = AppReducer.Reduce(
            started.State,
            new StagingSucceeded(tabId, "Staged a.txt", StatusSeverity.Info, StagingFollow.None));
        Assert.False(Running(done.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenAFailure_WhenItArrives_ThenTheTabIsFreeAgain()
    {
        var (state, tabId) = Opened(Modified("a.txt"));
        var started = AppReducer.Reduce(state, new StagingRequested(tabId, StagingAction.Stage));

        var failed = AppReducer.Reduce(started.State, new StagingFailed(tabId, "Could not stage: x"));

        Assert.False(Running(failed.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenAClosedTab_WhenAStagingResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened(Modified("a.txt"));

        var update = AppReducer.Reduce(
            state,
            new StagingSucceeded(new TabId(99), "x", StatusSeverity.Info, StagingFollow.None));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAStashRuns_WhenStaging_ThenItIsRefusedAndTheOtherWayRound()
    {
        var (state, tabId) = Opened(Modified("a.txt"));
        var running = WithRunning(state, tabId, RunningOperationKind.Stash);

        var refused = AppReducer.Reduce(running, new StagingRequested(tabId, StagingAction.Stage));
        Assert.Equal(StagingReducer.BlockedNotice, Assert.IsType<PostStatusEffect>(Assert.Single(refused.Effects)).Text);

        var staging = AppReducer.Reduce(state, new StagingRequested(tabId, StagingAction.Stage)).State;
        var stashRefused = AppReducer.Reduce(staging, new StashPushRequested(tabId, false));
        Assert.Equal(StashReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(stashRefused.Effects)).Text);
    }

    [Theory]
    [InlineData(RunningOperationKind.Integration)]
    [InlineData(RunningOperationKind.Pick)]
    [InlineData(RunningOperationKind.Rebase)]
    [InlineData(RunningOperationKind.Branches)]
    public void GivenAnOperationThatWritesTheTree_WhenStaging_ThenItIsRefused(RunningOperationKind kind)
    {
        var (state, tabId) = Opened(Modified("a.txt"));

        var refused = AppReducer.Reduce(WithRunning(state, tabId, kind), new StagingRequested(tabId, StagingAction.Stage));

        Assert.Equal(StagingReducer.BlockedNotice, Assert.IsType<PostStatusEffect>(Assert.Single(refused.Effects)).Text);
    }

    [Theory]
    [InlineData(RunningOperationKind.Sync)]
    [InlineData(RunningOperationKind.Tags)]
    [InlineData(RunningOperationKind.Remotes)]
    public void GivenAnOperationThatLeavesTheIndexAlone_WhenStaging_ThenItRuns(RunningOperationKind kind)
    {
        var (state, tabId) = Opened(Modified("a.txt"));

        var started = AppReducer.Reduce(WithRunning(state, tabId, kind), new StagingRequested(tabId, StagingAction.Stage));

        Assert.IsType<StagePathsEffect>(Assert.Single(started.Effects));
    }

    [Fact]
    public void GivenAConflictedFile_WhenTheTabIsClosedWhileTheDialogIsOpenAndYIsAnswered_ThenNothingIsStagedAndTheDialogCloses()
    {
        var (state, tabId) = Opened(Conflict("c.txt"));
        state = OpenResolveDialog(state, tabId);
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheResolveDialog_WhenYIsAnswered_ThenTheFileIsStagedAndTheTabIsBusy()
    {
        var (state, tabId) = Opened(Conflict("c.txt"));
        state = OpenResolveDialog(state, tabId);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["c.txt"], effect.Plan.Paths);
        Assert.True(Running(update.State, tabId).IsRunning(RunningOperationKind.Staging));
    }

    [Fact]
    public void GivenTheResolveDialog_WhenNIsAnswered_ThenNothingStarts()
    {
        var (state, tabId) = Opened(Conflict("c.txt"));
        state = OpenResolveDialog(state, tabId);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Empty(update.Effects);
        Assert.False(Running(update.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenAResolvedFile_WhenTheResultArrivesAfterTheStatusRead_ThenTheCursorMovesThroughTheRootReducer()
    {
        var (state, tabId) = Opened(Conflict("a.txt"), Conflict("b.txt"));
        state = AppReducer.Reduce(state, new TabCommand(tabId, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 1))).State;
        var started = AppReducer.Reduce(state, new StageResolvedRequested(tabId, "b.txt"));
        Assert.IsType<StagePathsEffect>(Assert.Single(started.Effects));

        // The tab refresher has applied the new status before the result arrives.
        var reloaded = AppReducer.Reduce(
            started.State,
            new TabCommand(tabId, new WorkingTreeLoadedCommand(Snapshot(Conflict("a.txt"), StagedModified("b.txt")))));
        var done = AppReducer.Reduce(
            reloaded.State,
            new StagingSucceeded(
                tabId,
                "Resolved and staged b.txt",
                StatusSeverity.Info,
                new StagingFollow(Resolved: new ResolvedFile("b.txt", "b.txt"))));

        Assert.Equal(
            "Resolved and staged b.txt (Staged list), 1 conflict left",
            Assert.IsType<PostStatusEffect>(Assert.Single(done.Effects)).Text);
        var tab = done.State.Workspace.FindTab(tabId)!;
        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "a.txt", 0), tab.WorkingTree!.Selection);
        Assert.False(tab.Running.IsBusy);
    }

    [Fact]
    public void GivenACommitWaitsForStageAll_WhenTheResultArrives_ThenItsCommandIsReducedAfterTheTabIsFree()
    {
        var (state, tabId) = Opened(Modified("a.txt"));
        var started = AppReducer.Reduce(state, new StageAllThenRequested(tabId, new RunActionCommand(() => { })));
        var follow = Assert.IsType<StagePathsEffect>(Assert.Single(started.Effects)).Follow;

        var done = AppReducer.Reduce(
            started.State,
            new StagingSucceeded(tabId, "Staged 1 file", StatusSeverity.Info, follow));

        Assert.Contains(done.Effects, effect => effect is RunActionEffect);
        Assert.False(Running(done.State, tabId).IsBusy);
    }

    // The dialog as the runner opens it: the reducer asks, the shell shows it.
    private static AppState OpenResolveDialog(AppState state, TabId tabId)
    {
        var asked = AppReducer.Reduce(state, new StagingRequested(tabId, StagingAction.Stage));
        var dialog = Assert.IsType<ShowConfirmEffect>(Assert.Single(asked.Effects));

        return AppReducer.Reduce(asked.State, new ConfirmRequestedCommand(dialog.Prompt, dialog.OnAccepted, null)).State;
    }

    private static RunningOperations Running(AppState state, TabId tabId) => state.Workspace.FindTab(tabId)!.Running;

    // The state another operation leaves while it runs in the tab.
    private static AppState WithRunning(AppState state, TabId tabId, RunningOperationKind kind)
    {
        var tab = state.Workspace.FindTab(tabId)!;
        var running = new RunningOperations([new RunningOperation(1, kind)], 1);
        var tabs = state.Workspace.Tabs.SetItem(0, tab with { Running = running });

        return state with { Workspace = state.Workspace with { Tabs = tabs } };
    }

    private static (AppState State, TabId TabId) Opened(params StatusEntry[] entries)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-staging-app-tests", "alpha");
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
