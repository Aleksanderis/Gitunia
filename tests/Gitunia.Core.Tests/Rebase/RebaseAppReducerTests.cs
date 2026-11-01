using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Rebase.RebaseTestData;

namespace Gitunia.Core.Tests.Rebase;

// The rebase commands through the root reducer: the running mark, the dialog and prompt chain, the tab that goes away.
public class RebaseAppReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenARequest_WhenReducing_ThenOnlyThatTabIsMarkedRunningUntilTheCountArrives()
    {
        var (state, first) = Opened("alpha");
        state = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta"))).State;
        var second = state.Workspace.Tabs[1].Id;

        var started = AppReducer.Reduce(state, OntoRequest(first)).State;
        var counted = AppReducer.Reduce(started, new RebaseMergesCounted(first, OntoTarget(), 0)).State;

        Assert.True(started.Workspace.FindTab(first)!.Running.IsRunning(RunningOperationKind.Rebase));
        Assert.False(started.Workspace.FindTab(second)!.Running.IsBusy);
        Assert.False(counted.Workspace.FindTab(first)!.Running.IsBusy);
    }

    [Fact]
    public void GivenARunningRebase_WhenAnotherIsAsked_ThenItIsRefusedFromStateAndNothingStarts()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, OntoRequest(tabId)).State;

        var update = AppReducer.Reduce(started, new QuickRebaseRequested(tabId, QuickRebaseKind.Drop, Target));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        Assert.Equal(RebaseReducer.BusyNotice, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenARunningRebase_WhenItFailsIsCancelledOrFinishes_ThenTheTabIsFreeForTheNextOne()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, OntoRequest(tabId)).State;
        var result = new OperationResult(OperationOutcome.Completed, Older);

        var failed = AppReducer.Reduce(started, new RebaseFailed(tabId, "boom")).State;
        var cancelled = AppReducer.Reduce(started, new RebaseCancelled(tabId)).State;
        var unplanned = AppReducer.Reduce(started, new QuickRebasePlanFailed(tabId, "boom")).State;
        var finished = AppReducer.Reduce(started, new RebaseFinished(tabId, OntoTarget(), result)).State;

        Assert.All(
            [failed, cancelled, unplanned, finished],
            next => Assert.False(next.Workspace.FindTab(tabId)!.Running.IsBusy));
    }

    [Fact]
    public void GivenTheCount_WhenItArrivesAndTheConfirmIsAnswered_ThenTheRebaseStartsAndMarksTheTab()
    {
        var (state, tabId) = Opened("alpha");
        var asked = AppReducer.Reduce(state, new RebaseMergesCounted(tabId, OntoTarget(), 0));
        state = AppReducer.Reduce(asked.State, ShowConfirm(asked)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Contains(update.Effects, effect => effect is RunRebaseEffect);
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsRunning(RunningOperationKind.Rebase));
    }

    [Fact]
    public void GivenTheConfirm_WhenTheTabIsClosedAndYIsAnswered_ThenNothingRuns()
    {
        var (state, tabId) = Opened("alpha");
        var asked = AppReducer.Reduce(state, new RebaseMergesCounted(tabId, OntoTarget(), 0));
        state = AppReducer.Reduce(asked.State, ShowConfirm(asked)).State;
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        Assert.Null(update.State.Shell.Confirm);
    }

    [Fact]
    public void GivenTheQuickConfirm_WhenTheTabIsClosedAndYIsAnswered_ThenNothingRuns()
    {
        var (state, tabId) = Opened("alpha");
        var asked = AppReducer.Reduce(state, new QuickRebasePlanned(tabId, Plan(QuickRebaseKind.Drop)));
        state = AppReducer.Reduce(asked.State, ShowConfirm(asked)).State;
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
    }

    [Fact]
    public void GivenAClosedTab_WhenARebaseResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened("alpha");

        var update = AppReducer.Reduce(state, new RebaseFailed(new TabId(99), "boom"));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAPromptOpenInTheShell_WhenTheCountArrives_ThenNoConfirmIsRequested()
    {
        var (state, tabId) = Opened("alpha");
        var answer = new QuickRebaseSubjectAnswer(tabId, Plan(QuickRebaseKind.Reword));
        var request = new PromptRequest("Rename", "x", "save", answer);
        state = AppReducer.Reduce(state, new PromptRequestedCommand(request)).State;

        var update = AppReducer.Reduce(state, new RebaseMergesCounted(tabId, OntoTarget(), 0));

        Assert.Equal(RebaseReducer.ChangedNotice, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
        Assert.DoesNotContain(update.Effects, effect => effect is ShowConfirmEffect);
    }

    [Fact]
    public void GivenAConfirmOpenInTheShell_WhenThePlanArrives_ThenItDoesNotReplaceTheDialog()
    {
        var (state, tabId) = Opened("alpha");
        var prompt = new ConfirmPrompt("Other", ["Other?"], "Undo", "Yes");
        state = AppReducer.Reduce(state, new ConfirmRequestedCommand(prompt, new RebaseCancelled(tabId), null)).State;

        var update = AppReducer.Reduce(state, new QuickRebasePlanned(tabId, Plan(QuickRebaseKind.Drop)));

        Assert.Equal(RebaseReducer.ChangedNotice, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenARewordPlan_WhenThePromptIsTypedAndAccepted_ThenTheConfirmCarriesTheTypedSubject()
    {
        var (state, tabId) = Opened("alpha");
        var planned = AppReducer.Reduce(state, new QuickRebasePlanned(tabId, Plan(QuickRebaseKind.Reword)));
        var shown = Assert.IsType<ShowPromptEffect>(Assert.Single(planned.Effects));
        state = AppReducer.Reduce(planned.State, new PromptRequestedCommand(shown.Request)).State;
        Assert.Equal(KeyMode.Insert, state.Shell.Mode);
        foreach (var character in "!")
        {
            state = AppReducer.Reduce(state, new PromptKeyPressedCommand(new KeyEvent(character.ToString()))).State;
        }

        var update = AppReducer.Reduce(state, new PromptAcceptedCommand());

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal("Add feature!", Assert.IsType<QuickRebaseConfirmed>(confirm.OnAccepted).Target.NewSubject);
        Assert.Null(update.State.Shell.Prompt);
    }

    [Fact]
    public void GivenTheRewordPrompt_WhenAnotherTabBecomesActive_ThenThePromptIsCancelled()
    {
        var (state, tabId) = Opened("alpha");
        var planned = AppReducer.Reduce(state, new QuickRebasePlanned(tabId, Plan(QuickRebaseKind.Reword)));
        var shown = Assert.IsType<ShowPromptEffect>(Assert.Single(planned.Effects));
        state = AppReducer.Reduce(planned.State, new PromptRequestedCommand(shown.Request)).State;

        var update = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta")));

        Assert.Null(update.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenTheRewordPrompt_WhenTheTabIsClosed_ThenThePromptIsCancelled()
    {
        var (state, tabId) = Opened("alpha");
        var planned = AppReducer.Reduce(state, new QuickRebasePlanned(tabId, Plan(QuickRebaseKind.Reword)));
        var shown = Assert.IsType<ShowPromptEffect>(Assert.Single(planned.Effects));
        state = AppReducer.Reduce(planned.State, new PromptRequestedCommand(shown.Request)).State;

        var update = AppReducer.Reduce(state, new CloseTabCommand(tabId));

        Assert.Null(update.State.Shell.Prompt);
    }

    [Fact]
    public void GivenAnEmptySubject_WhenAccepted_ThenThePromptOpensAgainWithTheTypedText()
    {
        var (state, tabId) = Opened("alpha");
        var planned = AppReducer.Reduce(state, new QuickRebasePlanned(tabId, Plan(QuickRebaseKind.Reword)));
        var shown = Assert.IsType<ShowPromptEffect>(Assert.Single(planned.Effects));
        state = AppReducer.Reduce(planned.State, new PromptRequestedCommand(shown.Request)).State;
        for (var index = 0; index < "Add feature".Length; index++)
        {
            state = AppReducer.Reduce(state, new PromptKeyPressedCommand(new KeyEvent(KeyNames.Backspace))).State;
        }

        var accepted = AppReducer.Reduce(state, new PromptAcceptedCommand());
        var reopened = Assert.IsType<ShowPromptEffect>(accepted.Effects.OfType<ShowPromptEffect>().Single());
        state = AppReducer.Reduce(accepted.State, new PromptRequestedCommand(reopened.Request)).State;

        Assert.Equal(string.Empty, state.Shell.Prompt!.Text);
        Assert.Equal(KeyMode.Insert, state.Shell.Mode);
        Assert.Contains(accepted.Effects, effect => effect is PostStatusEffect { Severity: StatusSeverity.Warning });
    }

    private static RebaseOntoRequested OntoRequest(TabId tabId) =>
        new(tabId, new RebaseSelection(null, null, Target));

    private static ConfirmRequestedCommand ShowConfirm(AppUpdate update)
    {
        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        var refusal = new StatusPostedCommand(confirm.UnavailableNotice, StatusSeverity.Warning, Start);

        return new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, refusal);
    }

    private static (AppState State, TabId TabId) Opened(string name)
    {
        var opened = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(Located(name))).State;
        var tabId = opened.Workspace.Tabs[0].Id;
        var head = AppReducer.Reduce(opened, new TabCommand(tabId, new HeadLoadedCommand(OnBranch()))).State;

        return (head, tabId);
    }

    private static RepositoryLocation Located(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-rebase-app-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
