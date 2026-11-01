using Gitunia.Core.Effects;
using Gitunia.Core.Integration;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Merge;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Reset;
using Xunit;
using static Gitunia.Core.Tests.Integration.IntegrationFixtures;

namespace Gitunia.Core.Tests.Integration;

// The reset, merge and operation reducers inside the root reducer: running marks, dropped late commands and the
// dialogs and prompt that outlive the tab.
public class IntegrationAppReducerTests
{
    private const string Target = "2222222222222222222222222222222222222222";

    [Fact]
    public void GivenAMerge_WhenReducing_ThenTheTabIsBusyUntilTheResultArrivesAndASecondIsRefused()
    {
        var (state, tabId) = Opened();

        var started = AppReducer.Reduce(state, new MergeRequested(tabId, MergeMode.Default, new("refs/heads/t", "t")));
        Assert.IsType<MergeBranchEffect>(Assert.Single(started.Effects));
        Assert.True(Running(started.State, tabId).IsRunning(RunningOperationKind.Integration));

        var second = AppReducer.Reduce(started.State, new MergeRequested(tabId, MergeMode.Default, new("x", "x")));
        Assert.IsType<PostStatusEffect>(Assert.Single(second.Effects));

        var done = AppReducer.Reduce(started.State, new IntegrationSucceeded(tabId, "Merged", StatusSeverity.Info));
        Assert.False(Running(done.State, tabId).IsBusy);
        Assert.IsType<RefreshTabEffect>(done.Effects[1]);
    }

    [Fact]
    public void GivenTheStatusRead_WhenItEnds_ThenTheBusyMarkEndsAndASoftResetStartsTheNextStep()
    {
        var (state, tabId) = Opened();
        var target = new ResetTarget(Target, "main", ResetMode.Soft);
        var preparing = AppReducer.Reduce(state, new ResetModeChosen(tabId, target));
        Assert.IsType<PrepareResetEffect>(Assert.Single(preparing.Effects));
        Assert.True(Running(preparing.State, tabId).IsRunning(RunningOperationKind.Integration));

        var resetting = AppReducer.Reduce(
            preparing.State,
            new ResetPrepared(tabId, target, RepositoryOperation.None, []));

        Assert.IsType<ResetBranchEffect>(Assert.Single(resetting.Effects));
        Assert.True(Running(resetting.State, tabId).IsRunning(RunningOperationKind.Integration));
    }

    [Fact]
    public void GivenAClosedTab_WhenAResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened();

        var update = AppReducer.Reduce(state, new IntegrationSucceeded(new TabId(99), "x", StatusSeverity.Info));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheResetModePrompt_WhenHIsPressed_ThenTheStatusReadStartsForTheHardReset()
    {
        var (state, tabId) = Opened();
        state = OpenPrompt(state, new ResetRequested(tabId, Target));
        Assert.Equal(KeyMode.Insert, state.Shell.Mode);

        var update = AppReducer.Reduce(state, new PromptKeyPressedCommand(new KeyEvent("h")));

        var prepare = Assert.IsType<PrepareResetEffect>(Assert.Single(update.Effects));
        Assert.Equal(new ResetTarget(Target, "main", ResetMode.Hard), prepare.Target);
        Assert.Null(update.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenTheResetModePrompt_WhenTheTabIsClosed_ThenThePromptClosesAndNothingRuns()
    {
        var (state, tabId) = Opened();
        state = OpenPrompt(state, new ResetRequested(tabId, Target));

        var update = AppReducer.Reduce(state, new CloseTabCommand(tabId));
        var late = AppReducer.Reduce(update.State, new PromptKeyPressedCommand(new KeyEvent("h")));

        Assert.Null(update.State.Shell.Prompt);
        Assert.Empty(late.Effects);
    }

    [Fact]
    public void GivenTheHardResetDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingIsResetAndTheDialogCloses()
    {
        var (state, tabId) = Opened();
        var target = new ResetTarget(Target, "main", ResetMode.Hard);
        state = OpenDialog(state, new ResetPrepared(tabId, target, RepositoryOperation.None, []));
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheHardResetDialog_WhenYIsAnswered_ThenTheResetStartsAndTheTabIsBusy()
    {
        var (state, tabId) = Opened();
        var target = new ResetTarget(Target, "main", ResetMode.Hard);
        state = OpenDialog(state, new ResetPrepared(tabId, target, RepositoryOperation.None, []));

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Equal(target, Assert.IsType<ResetBranchEffect>(Assert.Single(update.Effects)).Target);
        Assert.True(Running(update.State, tabId).IsRunning(RunningOperationKind.Integration));
    }

    [Fact]
    public void GivenTheHardResetDialog_WhenNIsAnswered_ThenNothingStarts()
    {
        var (state, tabId) = Opened();
        var target = new ResetTarget(Target, "main", ResetMode.Hard);
        state = OpenDialog(state, new ResetPrepared(tabId, target, RepositoryOperation.None, []));

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Empty(update.Effects);
        Assert.False(Running(update.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenTheAbortDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingIsAborted()
    {
        var (state, tabId) = Opened(Info(RepositoryOperation.Merge));
        state = OpenDialog(state, new OperationRequested(tabId, OperationAction.Abort));
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Empty(update.Effects);
        Assert.Null(update.State.Shell.Confirm);
    }

    [Fact]
    public void GivenTheSkipDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingIsSkipped()
    {
        var (state, tabId) = Opened(Info(RepositoryOperation.Rebase, "3333333"));
        state = OpenDialog(state, new OperationRequested(tabId, OperationAction.Skip));
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Empty(update.Effects);
        Assert.Null(update.State.Shell.Confirm);
    }

    [Fact]
    public void GivenTheAbortDialog_WhenYIsAnswered_ThenTheAbortStartsAndTheTabIsBusy()
    {
        var (state, tabId) = Opened(Info(RepositoryOperation.Merge));
        state = OpenDialog(state, new OperationRequested(tabId, OperationAction.Abort));

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.IsType<AbortOperationEffect>(Assert.Single(update.Effects));
        Assert.True(Running(update.State, tabId).IsRunning(RunningOperationKind.Integration));
    }

    [Fact]
    public void GivenTheOperationFinishedWhileTheSkipDialogIsOpen_WhenYIsAnswered_ThenNothingIsSkipped()
    {
        var (state, tabId) = Opened(Info(RepositoryOperation.Rebase, "3333333"));
        state = OpenDialog(state, new OperationRequested(tabId, OperationAction.Skip));
        state = AppReducer.Reduce(state, new TabCommand(tabId, new OperationLoadedCommand(null))).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Contains("moved on", post.Text, StringComparison.Ordinal);
    }

    // The dialog as the runner opens it: the reducer asks, the shell shows it.
    private static AppState OpenDialog(AppState state, IntegrationCommand command)
    {
        var asked = AppReducer.Reduce(state, command);
        var dialog = Assert.IsType<ShowConfirmEffect>(Assert.Single(asked.Effects));

        var requested = new ConfirmRequestedCommand(dialog.Prompt, dialog.OnAccepted, null);

        return AppReducer.Reduce(asked.State, requested).State;
    }

    private static AppState OpenPrompt(AppState state, IntegrationCommand command)
    {
        var asked = AppReducer.Reduce(state, command);
        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(asked.Effects));

        return AppReducer.Reduce(asked.State, new PromptRequestedCommand(prompt.Request)).State;
    }

    private static RunningOperations Running(AppState state, TabId tabId) => state.Workspace.FindTab(tabId)!.Running;

    private static (AppState State, TabId TabId) Opened(OperationInfo? operation = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-integration-app-tests", "alpha");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
        var tabId = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new HeadLoadedCommand(OnMain))).State;
        if (operation is not null)
        {
            state = AppReducer.Reduce(state, new TabCommand(tabId, new OperationLoadedCommand(operation))).State;
        }

        return (state, tabId);
    }
}
