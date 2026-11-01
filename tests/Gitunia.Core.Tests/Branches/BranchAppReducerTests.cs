using Gitunia.Core.Branches;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Branches.BranchTestData;

namespace Gitunia.Core.Tests.Branches;

// The branch commands through the root reducer: the running mark, the tab that goes away, the cursor that follows.
public class BranchAppReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenAConfirmedDelete_WhenReducing_ThenOnlyThatTabIsMarkedRunningUntilTheResult()
    {
        var (state, first) = Opened("alpha");
        state = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta"))).State;
        var second = state.Workspace.Tabs[1].Id;

        var started = AppReducer.Reduce(state, new BranchDeleteConfirmed(first, "topic", Topic, null)).State;
        var finished = AppReducer.Reduce(started, new BranchOperationSucceeded(first, "Deleted", null)).State;

        Assert.True(started.Workspace.FindTab(first)!.Running.IsRunning(RunningOperationKind.Branches));
        Assert.False(started.Workspace.FindTab(second)!.Running.IsBusy);
        Assert.False(finished.Workspace.FindTab(first)!.Running.IsBusy);
    }

    [Fact]
    public void GivenARunningBranchOperation_WhenAnotherIsAsked_ThenItIsRefusedFromStateAndNothingStarts()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, new BranchDeleteConfirmed(tabId, "topic", Topic, null)).State;

        var update = AppReducer.Reduce(started, new BranchRenameAccepted(tabId, "topic", "other"));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        var post = Assert.Single(update.Effects.OfType<PostStatusEffect>());
        Assert.Equal(BranchRefusals.BusyNotice, post.Text);
    }

    [Fact]
    public void GivenARunningOperation_WhenItFailsIsCancelledOrNeedsForce_ThenTheTabIsFreeForTheNextOne()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, new BranchDeleteConfirmed(tabId, "topic", Topic, null)).State;

        var failed = AppReducer.Reduce(started, new BranchOperationFailed(tabId, "boom")).State;
        var cancelled = AppReducer.Reduce(started, new BranchOperationCancelled(tabId)).State;
        var needsForce = AppReducer.Reduce(started, new BranchDeleteNeedsForce(tabId, "topic", Topic)).State;

        Assert.False(failed.Workspace.FindTab(tabId)!.Running.IsBusy);
        Assert.False(cancelled.Workspace.FindTab(tabId)!.Running.IsBusy);
        Assert.False(needsForce.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenAClosedTab_WhenABranchResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened("alpha");

        var update = AppReducer.Reduce(state, new BranchOperationSucceeded(new TabId(99), "Created", Topic));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenASuccessWithACommit_WhenReducing_ThenTheGraphCursorFollowsToIt()
    {
        var (state, tabId) = Opened("alpha");

        var update = AppReducer.Reduce(state, new BranchOperationSucceeded(tabId, "Created", Topic));

        Assert.True(ObjectId.TryParse(Topic, out var expected));
        Assert.Equal(expected, update.State.Workspace.FindTab(tabId)!.View.SelectedCommit);
        Assert.Contains(update.Effects, effect => effect is RefreshTabEffect);
    }

    [Fact]
    public void GivenAFailureWithACommitOrASuccessWithout_WhenReducing_ThenTheCursorStays()
    {
        var (state, tabId) = Opened("alpha");

        var failed = AppReducer.Reduce(state, new BranchOperationFailed(tabId, "boom"));
        var plain = AppReducer.Reduce(state, new BranchOperationSucceeded(tabId, "Set", null));

        Assert.Null(failed.State.Workspace.FindTab(tabId)!.View.SelectedCommit);
        Assert.Null(plain.State.Workspace.FindTab(tabId)!.View.SelectedCommit);
    }

    [Fact]
    public void GivenTheDeleteDialog_WhenYIsAnswered_ThenTheDeleteStartsAndTheTabIsMarkedRunning()
    {
        var (state, tabId) = AskedToDelete();

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Contains(update.Effects, effect => effect is DeleteBranchEffect { Force: false });
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenTheDeleteDialog_WhenNIsAnswered_ThenNothingStarts()
    {
        var (state, tabId) = AskedToDelete();

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Empty(update.Effects);
        Assert.False(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenTheDeleteDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingStartsAndTheDialogCloses()
    {
        var (state, tabId) = AskedToDelete();
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheUnmergedDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingStartsAndTheDialogCloses()
    {
        var (state, tabId) = Opened("alpha");
        var asked = AppReducer.Reduce(state, new BranchDeleteNeedsForce(tabId, "wip", Topic));
        state = AppReducer.Reduce(asked.State, ShowConfirm(asked)).State;
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheUnmergedDialog_WhenYIsAnswered_ThenTheForcedDeleteStarts()
    {
        var (state, tabId) = Opened("alpha");
        var asked = AppReducer.Reduce(state, new BranchDeleteNeedsForce(tabId, "wip", Topic));
        state = AppReducer.Reduce(asked.State, ShowConfirm(asked)).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Contains(update.Effects, effect => effect is DeleteBranchEffect { Force: true, Name: "wip" });
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenTheNamePrompt_WhenAnotherTabBecomesActive_ThenThePromptOfTheLeftTabIsCancelled()
    {
        var (state, tabId) = Opened("alpha");
        state = Run(state, new BranchCreateRequested(tabId, new BranchTarget(null, null, Topic), true));
        Assert.NotNull(state.Shell.Prompt);

        var update = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta")));

        Assert.Null(update.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenTheNamePrompt_WhenTheTabIsClosed_ThenThePromptIsCancelled()
    {
        var (state, tabId) = Opened("alpha");
        state = Run(state, new BranchCreateRequested(tabId, new BranchTarget(null, null, Topic), true));

        var update = AppReducer.Reduce(state, new CloseTabCommand(tabId));

        Assert.Null(update.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenTheNamePrompt_WhenAcceptedWithAName_ThenTheCreateStartsWithTheStartPointTheUserSaw()
    {
        var (state, tabId) = Opened("alpha");
        state = Run(state, new BranchCreateRequested(tabId, new BranchTarget(null, null, Topic), false));
        foreach (var character in "old")
        {
            state = AppReducer.Reduce(state, new PromptKeyPressedCommand(new KeyEvent(character.ToString()))).State;
        }

        var update = AppReducer.Reduce(state, new PromptAcceptedCommand());

        var create = Assert.IsType<CreateBranchEffect>(Assert.Single(update.Effects.OfType<BranchEffect>()));
        Assert.Equal("old", create.Name);
        Assert.Equal(Topic, create.StartPoint);
        Assert.False(create.Checkout);
        Assert.Null(update.State.Shell.Prompt);
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenTheDeleteList_WhenMovingWithLettersAndAccepting_ThenTheChosenBranchGetsTheDialog()
    {
        var (state, tabId) = Opened("alpha");
        state = AppReducer.Reduce(state, new TabCommand(tabId, new RefsLoadedCommand(
            Refs(Local("main", Main, current: true), Local("second", Main), Local("third", Main))))).State;
        state = Run(state, new BranchDeleteRequested(tabId, new BranchTarget(null, null, Main)));
        Assert.True(state.Shell.Prompt!.ListOnly);

        state = AppReducer.Reduce(state, new PromptKeyPressedCommand(new KeyEvent("j"))).State;
        state = AppReducer.Reduce(state, new PromptKeyPressedCommand(new KeyEvent("x"))).State;
        var update = AppReducer.Reduce(state, new PromptAcceptedCommand());

        Assert.Equal(string.Empty, update.State.Shell.Prompt?.Text ?? string.Empty);
        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal(new BranchDeleteConfirmed(tabId, "second", Main, null), confirm.OnAccepted);
    }

    private static (AppState State, TabId TabId) AskedToDelete()
    {
        var (state, tabId) = Opened("alpha");
        var accepted = new BranchDeleteConfirmed(tabId, "topic", Topic, null);
        var prompt = new ConfirmPrompt("Delete branch", ["Delete the branch topic?"], "Undo", "Delete branch");
        var asked = AppReducer.Reduce(state, new ConfirmRequestedCommand(prompt, accepted, null));

        return (asked.State, tabId);
    }

    private static ConfirmRequestedCommand ShowConfirm(AppUpdate update)
    {
        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        var refusal = new StatusPostedCommand(
            confirm.UnavailableNotice,
            StatusSeverity.Warning,
            Start);

        return new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, refusal);
    }

    // Reduces a command and feeds the prompt requests back like the runner does.
    private static AppState Run(AppState state, AppCommand command)
    {
        var update = AppReducer.Reduce(state, command);
        var next = update.State;
        foreach (var effect in update.Effects.OfType<ShowPromptEffect>())
        {
            next = AppReducer.Reduce(next, new PromptRequestedCommand(effect.Request)).State;
        }

        return next;
    }

    private static (AppState State, TabId TabId) Opened(string name)
    {
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(Located(name))).State;

        return (state, state.Workspace.Tabs[0].Id);
    }

    private static RepositoryLocation Located(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-branch-app-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
