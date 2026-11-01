using System.Collections.Immutable;
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
using Gitunia.Core.Worktrees;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Worktrees.WorktreeTestData;

namespace Gitunia.Core.Tests.Worktrees;

// The worktree commands through the root reducer: the running mark, the tab that goes away, the workspace commands.
public class WorktreeAppReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenAConfirmedRemoval_WhenReducing_ThenOnlyThatTabIsMarkedRunningUntilTheResult()
    {
        var (state, first) = Opened("alpha");
        state = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta"))).State;
        var second = state.Workspace.Tabs[1].Id;

        var started = AppReducer.Reduce(state, new WorktreeRemoveConfirmed(first, "x")).State;
        var finished = AppReducer.Reduce(started, new WorktreeOperationSucceeded(first, "Removed", null)).State;

        Assert.True(started.Workspace.FindTab(first)!.Running.IsRunning(RunningOperationKind.Worktrees));
        Assert.False(started.Workspace.FindTab(second)!.Running.IsBusy);
        Assert.False(finished.Workspace.FindTab(first)!.Running.IsBusy);
    }

    [Fact]
    public void GivenARunningWorktreeOperation_WhenAnotherIsAsked_ThenItIsRefusedFromStateAndNothingStarts()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, new WorktreeRemoveConfirmed(tabId, "x")).State;

        var update = AppReducer.Reduce(started, new WorktreePruneConfirmed(tabId));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        Assert.Contains(WorktreeTexts.BusyNotice, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenARunningOperation_WhenItFailsIsCancelledOrNeedsForce_ThenTheTabIsFreeForTheNextOne()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, new WorktreeRemoveConfirmed(tabId, "x")).State;

        var failed = AppReducer.Reduce(started, new WorktreeOperationFailed(tabId, "boom")).State;
        var cancelled = AppReducer.Reduce(started, new WorktreeOperationCancelled(tabId)).State;
        var needsForce = AppReducer.Reduce(started, new WorktreeRemoveNeedsForce(tabId, "x", ["a"])).State;

        Assert.False(failed.Workspace.FindTab(tabId)!.Running.IsBusy);
        Assert.False(cancelled.Workspace.FindTab(tabId)!.Running.IsBusy);
        Assert.False(needsForce.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenAClosedTab_WhenAWorktreeResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened("alpha");

        var update = AppReducer.Reduce(state, new WorktreeOperationSucceeded(new TabId(99), "Added", "x"));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenALocationResolvedForAClosedTab_WhenReducing_ThenNoTabOpensAndNothingIsSaid()
    {
        var (state, tabId) = Opened("alpha");
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;

        var update = AppReducer.Reduce(state, new WorktreeLocated(tabId, Located("beta"), false));

        Assert.Empty(update.State.Workspace.Tabs);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenALocationToOpen_WhenReducing_ThenANewTabOpensAndBecomesActive()
    {
        var (state, tabId) = Opened("alpha");

        var update = AppReducer.Reduce(state, new WorktreeLocated(tabId, Located("beta"), false));

        Assert.Equal(2, update.State.Workspace.Tabs.Length);
        Assert.NotEqual(tabId, update.State.Workspace.ActiveTabId);
        Assert.Contains(update.Effects, effect => effect is PostStatusEffect { Severity: StatusSeverity.Info });
    }

    [Fact]
    public void GivenALocationToSwitch_WhenReducing_ThenTheTabMovesToTheWorktreeAndKeepsItsId()
    {
        var (state, tabId) = Opened("alpha");

        var update = AppReducer.Reduce(state, new WorktreeLocated(tabId, Located("beta"), true));

        var tab = Assert.Single(update.State.Workspace.Tabs);
        Assert.Equal(tabId, tab.Id);
        Assert.Equal(Located("beta").RootPath, tab.Location.RootPath);
    }

    [Fact]
    public void GivenAnOverviewForATabThatMovedMeanwhile_WhenReducing_ThenTheStateIsUnchanged()
    {
        var (state, tabId) = Opened("alpha");
        var statuses = ImmutableDictionary<string, WorktreeStatus>.Empty.Add("p", new WorktreeStatus(1, 0, 0, 0, 0));

        var stale = AppReducer.Reduce(state, new WorktreeStatusesRead(tabId, Located("elsewhere"), statuses));
        var fresh = AppReducer.Reduce(state, new WorktreeStatusesRead(tabId, Located("alpha"), statuses));

        Assert.Null(stale.State.Workspace.FindTab(tabId)!.WorktreeStatuses);
        Assert.Equal(statuses, fresh.State.Workspace.FindTab(tabId)!.WorktreeStatuses);
    }

    [Fact]
    public void GivenTheRemoveDialog_WhenYIsAnswered_ThenTheRemovalStartsAndTheTabIsMarkedRunning()
    {
        var (state, tabId) = AskedToRemove();

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Contains(update.Effects, effect => effect is RemoveWorktreeEffect { Force: false });
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenTheRemoveDialog_WhenNIsAnswered_ThenNothingStarts()
    {
        var (state, tabId) = AskedToRemove();

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Empty(update.Effects);
        Assert.False(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenTheRemoveDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingStartsAndTheDialogCloses()
    {
        var (state, tabId) = AskedToRemove();
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheDirtyRemoveDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingStartsAndTheDialogCloses()
    {
        var (state, tabId) = Opened("alpha");
        var asked = AppReducer.Reduce(state, new WorktreeRemoveNeedsForce(tabId, "x", ["a"]));
        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(asked.Effects));
        var refusal = new StatusPostedCommand(confirm.UnavailableNotice, StatusSeverity.Warning, Start);
        var request = new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, refusal);
        state = AppReducer.Reduce(asked.State, request).State;
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheDirtyRemoveDialog_WhenYIsAnswered_ThenTheForcedRemovalStarts()
    {
        var (state, tabId) = Opened("alpha");
        var asked = AppReducer.Reduce(state, new WorktreeRemoveNeedsForce(tabId, "x", ["a"]));
        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(asked.Effects));
        var request = new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, null);
        state = AppReducer.Reduce(asked.State, request).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Contains(update.Effects, effect => effect is RemoveWorktreeEffect { Force: true });
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenThePathPrompt_WhenAnotherTabBecomesActiveOrTheTabCloses_ThenThePromptIsCancelled()
    {
        var (state, tabId) = Opened("alpha");
        state = Run(state, new WorktreeAddRequested(tabId, new WorktreeTarget(null, null, null, null, null)));
        Assert.NotNull(state.Shell.Prompt);

        var switched = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta")));
        var closed = AppReducer.Reduce(state, new CloseTabCommand(tabId));

        Assert.Null(switched.State.Shell.Prompt);
        Assert.Null(closed.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, closed.State.Shell.Mode);
    }

    [Fact]
    public void GivenTheBranchPrompt_WhenAcceptedWithAName_ThenThePathPromptOpensWithTheDefaultDirectory()
    {
        var (state, tabId) = Opened("alpha");
        state = Run(state, new WorktreeAddRequested(tabId, new WorktreeTarget(null, null, null, null, Topic)));
        foreach (var character in "fresh")
        {
            state = AppReducer.Reduce(state, new PromptKeyPressedCommand(new KeyEvent(character.ToString()))).State;
        }

        var update = AppReducer.Reduce(state, new PromptAcceptedCommand());
        var next = Run(state, new PromptAcceptedCommand());

        Assert.Equal("Worktree path", next.Shell.Prompt!.Title);
        Assert.Contains(update.Effects, effect => effect is ShowPromptEffect);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(Located("alpha").RootPath)!, "fresh"), next.Shell.Prompt.Text);
    }

    private static (AppState State, TabId TabId) AskedToRemove()
    {
        var (state, tabId) = Opened("alpha");
        var accepted = new WorktreeRemoveConfirmed(tabId, "x");
        var prompt = new ConfirmPrompt("Remove worktree", ["Remove the worktree x?"], "Undo", "Remove worktree");
        var asked = AppReducer.Reduce(state, new ConfirmRequestedCommand(prompt, accepted, null));

        return (asked.State, tabId);
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
        var root = Path.Combine(Path.GetTempPath(), "gitunia-worktree-app-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
