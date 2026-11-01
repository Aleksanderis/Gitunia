using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Stash;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Xunit;

namespace Gitunia.Core.Tests.Stash;

// The stash reducer inside the root reducer: running marks, dropped late commands and the open dialog.
public class StashAppReducerTests
{
    private static readonly StashEntry Entry = new(0, "stash@{0}", "abc123", "wip");

    [Fact]
    public void GivenARequest_WhenReducing_ThenTheTabIsMarkedBusyUntilTheResultArrives()
    {
        var (state, tabId) = Opened();

        var applied = AppReducer.Reduce(state, new StashApplyRequested(tabId, "abc123", false));
        Assert.IsType<ApplyStashEffect>(Assert.Single(applied.Effects));
        Assert.True(applied.State.Workspace.FindTab(tabId)!.Running.IsRunning(RunningOperationKind.Stash));

        var busy = AppReducer.Reduce(applied.State, new StashApplyRequested(tabId, "abc123", true));
        Assert.IsType<PostStatusEffect>(Assert.Single(busy.Effects));

        var done = AppReducer.Reduce(applied.State, new StashOperationSucceeded(tabId, "Applied", StatusSeverity.Info));
        Assert.False(done.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenTheRestoreCheck_WhenItEndsWithCreates_ThenTheRestoreStartsAndTheTabStaysBusy()
    {
        var (state, tabId) = Opened();
        var file = new StashFileChange(
            new CommitFileChange("a", null, FileChangeKind.Added, null),
            StashFileOrigin.Tracked);
        var target = new StashFileTarget(Entry, file);
        var checking = AppReducer.Reduce(state, new StashRestoreFileRequested(tabId, target));
        Assert.IsType<CheckStashRestoreEffect>(Assert.Single(checking.Effects));

        var restoring = AppReducer.Reduce(
            checking.State,
            new StashRestoreChecked(tabId, target, StashRestoreState.Creates));

        Assert.IsType<RestoreStashFileEffect>(Assert.Single(restoring.Effects));
        Assert.True(restoring.State.Workspace.FindTab(tabId)!.Running.IsRunning(RunningOperationKind.Stash));
    }

    [Fact]
    public void GivenAClosedTab_WhenAStashResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened();

        var update = AppReducer.Reduce(state, new StashOperationSucceeded(new TabId(99), "x", StatusSeverity.Info));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenDropDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingIsDroppedAndTheDialogCloses()
    {
        var (state, tabId) = Opened();
        state = OpenDropDialog(state, tabId);
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenDropDialog_WhenYIsAnswered_ThenTheDropStartsAndTheTabIsBusy()
    {
        var (state, tabId) = Opened();
        state = OpenDropDialog(state, tabId);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        var drop = Assert.IsType<DropStashEffect>(Assert.Single(update.Effects));
        Assert.Equal(Entry, drop.Entry);
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsRunning(RunningOperationKind.Stash));
    }

    [Fact]
    public void GivenAnOpenDropDialog_WhenNIsAnswered_ThenNothingStarts()
    {
        var (state, tabId) = Opened();
        state = OpenDropDialog(state, tabId);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Empty(update.Effects);
        Assert.False(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    // The dialog as the runner opens it: the reducer asks, the shell shows it.
    private static AppState OpenDropDialog(AppState state, TabId tabId)
    {
        var asked = AppReducer.Reduce(state, new StashDropRequested(tabId, "abc123"));
        var dialog = Assert.IsType<ShowConfirmEffect>(Assert.Single(asked.Effects));

        return AppReducer.Reduce(asked.State, new ConfirmRequestedCommand(dialog.Prompt, dialog.OnAccepted, null)).State;
    }

    private static (AppState State, TabId TabId) Opened()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-stash-app-tests", "alpha");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
        var tabId = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new StashesLoadedCommand([Entry]))).State;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new FocusPaneCommand(PaneKind.Sidebar))).State;

        return (state, tabId);
    }
}
