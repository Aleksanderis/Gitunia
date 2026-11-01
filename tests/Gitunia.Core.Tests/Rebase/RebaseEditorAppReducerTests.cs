using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
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

// The editor commands through the root reducer: the running mark, the session in the shell state, the tab that goes away.
public class RebaseEditorAppReducerTests
{
    private const string First = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Second = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenAnOpenRequest_WhenReducing_ThenOnlyThatTabIsMarkedRunningUntilTheRangeArrives()
    {
        var (state, first) = Opened("alpha");
        state = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta"))).State;
        var second = state.Workspace.Tabs[1].Id;

        var started = AppReducer.Reduce(state, Open(first)).State;
        var read = AppReducer.Reduce(started, new RebaseEditorRangeRead(first, 1, Todo(first), Target)).State;

        Assert.True(started.Workspace.FindTab(first)!.Running.IsRunning(RunningOperationKind.Rebase));
        Assert.False(started.Workspace.FindTab(second)!.Running.IsBusy);
        Assert.True(started.Shell.RebaseEditor.IsReading);
        Assert.False(read.Workspace.FindTab(first)!.Running.IsBusy);
        Assert.NotNull(read.Shell.RebaseEditor.Editor);
    }

    [Fact]
    public void GivenARunningRebase_WhenTheEditorIsOpened_ThenItIsRefusedFromStateAndNothingStarts()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, new RebaseOntoRequested(tabId, new RebaseSelection(null, null, Target)));

        var update = AppReducer.Reduce(started.State, Open(tabId));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        Assert.Equal(
            "Busy: another operation of this tab is still running",
            Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenARunningEditorRead_WhenAQuickRebaseIsAsked_ThenItIsRefusedAsBusy()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, Open(tabId)).State;

        var update = AppReducer.Reduce(started, new QuickRebaseRequested(tabId, QuickRebaseKind.Drop, Target));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
    }

    [Fact]
    public void GivenEveryResultOfTheEditor_WhenItArrives_ThenTheTabIsFreeForTheNextOperation()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, Open(tabId)).State;
        var finished = new InteractiveRebaseResult(OperationOutcome.Completed, Target, Tip);
        RebaseEditorCommand[] results =
        [
            new RebaseEditorRangeRead(tabId, 1, Todo(tabId), Target),
            new RebaseEditorMessageRead(tabId, 1, First, "m"),
            new RebaseEditorReadFailed(tabId, "boom", StatusSeverity.Error),
            new RebaseEditorReadCancelled(tabId),
            new RebaseEditorFinished(tabId, Todo(tabId), finished),
            new RebaseEditorNotStarted(tabId, Todo(tabId), "no"),
        ];

        Assert.All(
            results,
            result => Assert.False(
                AppReducer.Reduce(started, result).State.Workspace.FindTab(tabId)!.Running.IsBusy));
    }

    [Fact]
    public void GivenTheEditorOpen_WhenConfirmedAndAnswered_ThenTheTodoRunsMarksTheTabAndTheEditorIsGone()
    {
        var (state, tabId) = OpenEditor("alpha");
        var asked = AppReducer.Reduce(state, Press(tabId, ActionIds.RebaseEditorStart));
        state = AppReducer.Reduce(asked.State, ShowConfirm(asked)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Contains(update.Effects, effect => effect is RunInteractiveRebaseEffect);
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsRunning(RunningOperationKind.Rebase));
        Assert.Null(update.State.Shell.RebaseEditor.Editor);
    }

    [Fact]
    public void GivenTheConfirm_WhenDeclined_ThenTheEditorStaysOpenAndNothingRuns()
    {
        var (state, tabId) = OpenEditor("alpha");
        var asked = AppReducer.Reduce(state, Press(tabId, ActionIds.RebaseEditorStart));
        state = AppReducer.Reduce(asked.State, ShowConfirm(asked)).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        Assert.NotNull(update.State.Shell.RebaseEditor.Editor);
        Assert.False(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenTheConfirm_WhenTheTabIsClosedAndYIsAnswered_ThenNothingRuns()
    {
        var (state, tabId) = OpenEditor("alpha");
        var asked = AppReducer.Reduce(state, Press(tabId, ActionIds.RebaseEditorStart));
        state = AppReducer.Reduce(asked.State, ShowConfirm(asked)).State;
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.DoesNotContain(update.Effects, effect => effect is RunInteractiveRebaseEffect);
        Assert.Null(update.State.Shell.RebaseEditor.Editor);
    }

    [Fact]
    public void GivenTheEditorOpen_WhenItsTabIsClosed_ThenTheEditorClosesAndNormalModeIsBack()
    {
        var (state, tabId) = OpenEditor("alpha");
        state = AppReducer.Reduce(state, new RebaseEditorActionPressed(tabId, ActionIds.RebaseEditorReword, null, 10))
            .State;
        state = AppReducer.Reduce(state, new RebaseEditorMessageRead(tabId, 2, First, "text")).State;
        Assert.True(state.Shell.RebaseEditor.IsEditingMessage);

        var update = AppReducer.Reduce(state, new CloseTabCommand(tabId));

        Assert.False(update.State.Shell.RebaseEditor.IsOpen);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenAnotherTabClosed_WhenTheEditorIsOpen_ThenItStaysOpen()
    {
        var (state, tabId) = OpenEditor("alpha");
        state = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta"))).State;
        var other = state.Workspace.Tabs[1].Id;

        var update = AppReducer.Reduce(state, new CloseTabCommand(other));

        Assert.Equal(tabId, update.State.Shell.RebaseEditor.Editor!.TabId);
    }

    [Fact]
    public void GivenAReadInFlight_WhenTheTabIsClosedAndTheResultArrives_ThenTheReadEndsSoTheNextOpenWorks()
    {
        var (state, tabId) = Opened("alpha");
        state = AppReducer.Reduce(state, Open(tabId)).State;
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.True(state.Shell.RebaseEditor.IsReading);

        var update = AppReducer.Reduce(state, new RebaseEditorRangeRead(tabId, 1, Todo(tabId), Target));

        Assert.False(update.State.Shell.RebaseEditor.IsReading);
        Assert.Null(update.State.Shell.RebaseEditor.Editor);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheEditorOpen_WhenAPlainRebaseConfirmIsPrepared_ThenItDoesNotOpenOverTheEditor()
    {
        var (state, tabId) = OpenEditor("alpha");
        var target = OntoTarget();

        var update = AppReducer.Reduce(state, new RebaseMergesCounted(tabId, target, 0));

        Assert.DoesNotContain(update.Effects, effect => effect is ShowConfirmEffect);
    }

    private static RebaseEditorOpenRequested Open(TabId tabId) =>
        new(tabId, new RebaseSelection(null, null, Target), false);

    private static RebaseEditorActionPressed Press(TabId tabId, ActionId action) => new(tabId, action, null, 10);

    private static RebaseEditorState Todo(TabId tabId) => RebaseEditorState.Create(
        tabId,
        "main",
        "topic",
        RebaseBase.OntoCommit(Target),
        Tip,
        [new(First, "one"), new(Second, "two")]);

    private static (AppState State, TabId TabId) OpenEditor(string name)
    {
        var (state, tabId) = Opened(name);
        state = AppReducer.Reduce(state, Open(tabId)).State;
        state = AppReducer.Reduce(state, new RebaseEditorRangeRead(tabId, 1, Todo(tabId), Target)).State;

        return (state, tabId);
    }

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
        var root = Path.Combine(Path.GetTempPath(), "gitunia-rebase-editor-app-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
