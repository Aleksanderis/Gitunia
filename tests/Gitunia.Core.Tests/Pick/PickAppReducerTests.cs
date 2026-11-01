using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Pick;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Pick;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Pick.PickTestData;

namespace Gitunia.Core.Tests.Pick;

// The pick commands through the root reducer: the running mark, the shell-state question, the tab that goes away.
public class PickAppReducerTests
{
    [Fact]
    public void GivenARequest_WhenReducing_ThenTheTabIsMarkedRunningUntilTheCommitsAreRead()
    {
        var (state, tabId) = Opened("alpha");

        var started = AppReducer.Reduce(state, Request(tabId)).State;
        var read = AppReducer.Reduce(started, new PickCommitsRead(tabId, PickKind.CherryPick, [Plain(First)]));

        Assert.True(Running(started, tabId).IsRunning(RunningOperationKind.Pick));
        Assert.True(Running(read.State, tabId).IsRunning(RunningOperationKind.Pick));
        Assert.Single(read.Effects.OfType<RunPickEffect>());
    }

    [Fact]
    public void GivenARunningPick_WhenAnotherIsAsked_ThenItIsRefusedFromStateAndNothingStarts()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, Request(tabId)).State;

        var update = AppReducer.Reduce(started, Request(tabId));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        Assert.Equal(PickReducer.BusyNotice, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenARunningPick_WhenItFailsIsCancelledOrFinishes_ThenTheTabIsFreeForTheNextOne()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, Request(tabId)).State;
        var result = new PickResult(PickOutcome.Completed, Second, First);

        var failed = AppReducer.Reduce(started, new PickFailed(tabId, "boom")).State;
        var cancelled = AppReducer.Reduce(started, new PickCancelled(tabId)).State;
        var unread = AppReducer.Reduce(started, new PickReadFailed(tabId, "boom")).State;
        var finished = AppReducer.Reduce(started, new PickFinished(tabId, PickKind.Revert, result, "main", "x")).State;

        Assert.All([failed, cancelled, unread, finished], next => Assert.False(Running(next, tabId).IsBusy));
    }

    [Fact]
    public void GivenAMergeCommit_WhenTheCommitsAreRead_ThenTheQuestionIsInTheShellStateAndTheTabIsFree()
    {
        var (state, tabId) = Opened("alpha");
        var started = AppReducer.Reduce(state, Request(tabId)).State;

        var update = AppReducer.Reduce(started, new PickCommitsRead(tabId, PickKind.Revert, [Merge(First)]));

        Assert.Equal(tabId, update.State.Shell.Mainline!.TabId);
        Assert.False(Running(update.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenTheQuestion_WhenAParentIsChosen_ThenItLeavesTheShellStateAndThePickRuns()
    {
        var state = Asked(out var tabId);

        var update = AppReducer.Reduce(state, new PickMainlineChosen(tabId, 2));

        Assert.Null(update.State.Shell.Mainline);
        Assert.Equal(2, Assert.Single(update.Effects.OfType<RunPickEffect>()).Mainline);
        Assert.True(Running(update.State, tabId).IsRunning(RunningOperationKind.Pick));
    }

    [Fact]
    public void GivenTheQuestion_WhenAnOutOfRangeParentIsChosen_ThenItStaysInTheShellState()
    {
        var state = Asked(out var tabId);

        var update = AppReducer.Reduce(state, new PickMainlineChosen(tabId, 7));

        Assert.NotNull(update.State.Shell.Mainline);
        Assert.Single(update.Effects.OfType<PostStatusEffect>());
    }

    [Fact]
    public void GivenTheQuestion_WhenAnotherTabBecomesActive_ThenTheQuestionIsDroppedAndTheModeReturns()
    {
        var state = Asked(out _);
        state = AppReducer.Reduce(state, new SetKeyModeCommand(KeyMode.Insert)).State;

        var update = AppReducer.Reduce(state, new OpenRepositoryCommand(Located("beta")));

        Assert.Null(update.State.Shell.Mainline);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenTheQuestion_WhenTheTabIsClosed_ThenTheQuestionIsDropped()
    {
        var state = Asked(out var tabId);

        var update = AppReducer.Reduce(state, new CloseTabCommand(tabId));

        Assert.Null(update.State.Shell.Mainline);
    }

    [Fact]
    public void GivenAClosedTab_WhenAPickResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened("alpha");

        var update = AppReducer.Reduce(state, new PickFinished(
            new TabId(99),
            PickKind.Revert,
            new PickResult(PickOutcome.Completed, Second, First),
            "main",
            "x"));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAClosedTab_WhenTheMainlineIsAnswered_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened("alpha");

        var update = AppReducer.Reduce(state, new PickMainlineChosen(new TabId(99), 1));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    private static PickRequested Request(TabId tabId) =>
        new(tabId, PickKind.CherryPick, new PickTarget(SidebarRowKind.LocalBranch, [First]));

    private static RunningOperations Running(AppState state, TabId tabId) => state.Workspace.FindTab(tabId)!.Running;

    // A tab with a branch checked out and the question open for a merge commit.
    private static AppState Asked(out TabId tabId)
    {
        var (state, id) = Opened("alpha");
        tabId = id;
        var started = AppReducer.Reduce(state, Request(id)).State;

        return AppReducer.Reduce(started, new PickCommitsRead(id, PickKind.Revert, [Merge(First)])).State;
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
        var root = Path.Combine(Path.GetTempPath(), "gitunia-pick-app-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
