using Gitunia.Core.Creation;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Core.Tests.Picker;
using Gitunia.Core.Workspaces;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces;

// The tab, pin and picker actions through the root reducer, as the terminal layer sends them.
public class WorkspaceActionReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenThreeTabs_WhenTabNextRunsWithANumber_ThenThatTabIsActive()
    {
        var state = Tabs("a", "b", "c");

        var update = Press(state, ActionIds.TabNext, count: 2);

        Assert.Equal(state.Workspace.Tabs[1].Id, update.State.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenTheGoToAction_WhenItRuns_ThenThatTabIsActive()
    {
        var state = Tabs("a", "b", "c");

        var update = Press(state, ActionIds.TabGoTo(1));

        Assert.Equal(state.Workspace.Tabs[0].Id, update.State.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenATabIsSelected_ThenThePickerClosesAndTheTabIsActive()
    {
        var state = AppReducer.Reduce(Tabs("a", "b"), new PickerOpenRequested(null, string.Empty, Start)).State;

        var update = Press(state, ActionIds.TabPrevious);

        Assert.False(update.State.Shell.Picker.IsOpen);
        Assert.Equal(state.Workspace.Tabs[0].Id, update.State.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenNoTabs_WhenATabIsSelected_ThenNothingHappens()
    {
        var state = AppReducer.Reduce(Empty(), new PickerOpenRequested(null, string.Empty, Start)).State;

        var update = Press(state, ActionIds.TabNext, count: 3);

        Assert.True(update.State.Shell.Picker.IsOpen);
        Assert.Empty(update.State.Workspace.Tabs);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenPickerOpenRunsAgain_ThenTheStateIsKept()
    {
        var state = AppReducer.Reduce(Empty(), new PickerOpenRequested(null, string.Empty, Start)).State;
        state = AppReducer.Reduce(state, new TypeTextCommand("abc")).State;

        var update = Press(state, ActionIds.PickerOpen);

        Assert.Equal("abc", update.State.Shell.Picker.State!.Query);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenTheMouseDoubleClickActionRuns_ThenThePickerHandlesIt()
    {
        var state = AppReducer.Reduce(Empty(), new PickerOpenRequested(null, string.Empty, Start)).State;
        state = AppReducer.Reduce(state, new TypeTextCommand("/nowhere")).State;

        var update = Press(state, ActionIds.PickerAccept);

        Assert.Single(update.Effects.OfType<DiscoverRepositoryEffect>());
        Assert.True(WorkspaceActionReducer.Owns(ActionIds.PickerAccept, state));
    }

    [Fact]
    public void GivenAnOpenPicker_WhenTheMovementActionsRun_ThenTheHighlightMoves()
    {
        var state = AppReducer.Reduce(Empty(), new PickerOpenRequested(null, string.Empty, Start)).State;
        state = AppReducer.Reduce(state, new PickerScanFinished([PickerFixtures.PathOf("a"), PickerFixtures.PathOf("b")])).State;

        var next = Press(state, ActionIds.PickerNext).State;
        var back = Press(next, ActionIds.PickerPrevious).State;

        Assert.Equal(1, next.Shell.Picker.State!.Highlight);
        Assert.Equal(0, back.Shell.Picker.State!.Highlight);
    }

    [Fact]
    public void GivenAClosedPicker_WhenPickerActionsRun_ThenTheyAreNotOwned()
    {
        var state = Tabs("a");

        Assert.False(WorkspaceActionReducer.Owns(ActionIds.PickerAccept, state));
        Assert.False(WorkspaceActionReducer.Owns(ActionIds.Cancel, state));
        Assert.True(WorkspaceActionReducer.Owns(ActionIds.PickerOpen, state));
    }

    [Fact]
    public void GivenAnUnsentMessage_WhenClosingTheTab_ThenTheFirstCloseOnlyWarnsAndTheSecondCloses()
    {
        var state = Tabs("a", "b");
        var active = state.Workspace.ActiveTabId!.Value;

        var first = Press(state, ActionIds.TabClose, unsent: true);
        Assert.Equal(2, first.State.Workspace.Tabs.Length);
        Assert.StartsWith("Unsent commit message in b", first.State.Shell.TabActions.Notice, StringComparison.Ordinal);
        Assert.Contains("close again to lose it", first.State.Shell.TabActions.Notice, StringComparison.Ordinal);

        var second = Press(first.State, ActionIds.TabClose, unsent: true);
        Assert.Null(second.State.Workspace.FindTab(active));
        Assert.Empty(second.State.Shell.TabActions.Notice);
    }

    [Fact]
    public void GivenAnUnsentMessage_WhenAnotherActionComesBetweenTheCloses_ThenTheGuardWarnsAgain()
    {
        var state = Tabs("a", "b");
        var first = Press(state, ActionIds.TabClose, unsent: true);
        var between = Press(first.State, ActionIds.TabPrevious, unsent: true);

        var again = Press(between.State, ActionIds.TabClose, unsent: true);

        Assert.Equal(2, again.State.Workspace.Tabs.Length);
        Assert.NotEmpty(again.State.Shell.TabActions.Notice);
    }

    [Fact]
    public void GivenNoUnsentMessage_WhenClosingTheTab_ThenItClosesAtOnce()
    {
        var state = Tabs("a", "b");

        var update = Press(state, ActionIds.TabClose);

        Assert.Single(update.State.Workspace.Tabs);
    }

    [Fact]
    public void GivenTheLastTab_WhenClosingIt_ThenThePickerOpens()
    {
        var state = Tabs("a");

        var update = Press(state, ActionIds.TabClose);

        Assert.Empty(update.State.Workspace.Tabs);
        Assert.True(update.State.Shell.Picker.IsOpen);
    }

    [Fact]
    public void GivenAnotherTabLeft_WhenClosingATab_ThenThePickerStaysClosed()
    {
        var update = Press(Tabs("a", "b"), ActionIds.TabClose);

        Assert.False(update.State.Shell.Picker.IsOpen);
    }

    [Fact]
    public void GivenATab_WhenRenaming_ThenThePromptOpensForIt()
    {
        var state = Tabs("a");

        var update = Press(state, ActionIds.TabRename);

        var request = Assert.Single(update.Effects.OfType<ShowPromptEffect>()).Request;
        Assert.Equal(state.Workspace.ActiveTabId, request.Owner);
    }

    [Fact]
    public void GivenNoTab_WhenRenaming_ThenTheActionIsNotOwnedAndNoPromptOpens()
    {
        var state = Empty();

        Assert.False(WorkspaceActionReducer.Owns(ActionIds.TabRename, state));
        Assert.Empty(Press(state, ActionIds.TabRename).Effects);
    }

    [Fact]
    public void GivenATab_WhenPinningTwice_ThenItIsPinnedThenUnpinnedWithANoticeEachTime()
    {
        var state = Tabs("a");

        var pinned = Press(state, ActionIds.RepositoryPin);
        Assert.StartsWith("pinned a:", pinned.State.Shell.TabActions.Notice, StringComparison.Ordinal);
        Assert.Single(pinned.State.Workspace.Pins);

        var unpinned = Press(pinned.State, ActionIds.RepositoryPin);
        Assert.Equal("unpinned a", unpinned.State.Shell.TabActions.Notice);
        Assert.Empty(unpinned.State.Workspace.Pins);
    }

    [Fact]
    public void GivenNoTab_WhenPinning_ThenTheNoticeSaysThereIsNothingToPin()
    {
        var update = Press(Empty(), ActionIds.RepositoryPin);

        Assert.Equal("no repository to pin", update.State.Shell.TabActions.Notice);
    }

    [Fact]
    public void GivenAMissingTab_WhenOpeningDetails_ThenThePickerLocatesItStartingInItsFolder()
    {
        var state = Tabs("gone");
        var missing = state.Workspace.Tabs[0];
        state = state with { Workspace = state.Workspace with { Tabs = [missing with { IsMissing = true }] } };

        var update = Press(state, ActionIds.GraphOpenDetails);

        var picker = update.State.Shell.Picker.State!;
        Assert.Equal(missing.Id, picker.LocatingTabId);
        Assert.Equal(PickerFixtures.PathOf("code") + Path.DirectorySeparatorChar, picker.Query);
    }

    [Fact]
    public void GivenAPromptOfThePicker_WhenPickerActionsRun_ThenTheyDoNotActOnThePicker()
    {
        var state = AppReducer.Reduce(Empty(), new PickerOpenRequested(null, string.Empty, Start)).State;
        state = AppReducer.Reduce(state, new PickerScanFinished([PickerFixtures.PathOf("a"), PickerFixtures.PathOf("b")])).State;
        state = Prompted(state);

        var next = Press(state, ActionIds.PickerNext);

        Assert.True(WorkspaceActionReducer.Owns(ActionIds.PickerNext, state));
        Assert.Equal(0, next.State.Shell.Picker.State!.Highlight);
        Assert.False(WorkspaceActionReducer.Owns(ActionIds.Cancel, state));
        Assert.False(WorkspaceActionReducer.Owns(ActionIds.InputAccept, state));
    }

    [Fact]
    public void GivenAPromptOfThePicker_WhenPickerAcceptRuns_ThenThePromptIsAccepted()
    {
        var state = Prompted(AppReducer.Reduce(Empty(), new PickerOpenRequested(null, string.Empty, Start)).State);

        var update = Press(state, ActionIds.PickerAccept);

        Assert.Null(update.State.Shell.Prompt);
    }

    [Fact]
    public void GivenThreeTabs_WhenTheMoveActionsRunWithAndWithoutACount_ThenTheActiveTabMovesAndStaysActive()
    {
        var state = Tabs("alpha", "beta", "gamma");
        var active = state.Workspace.ActiveTabId;

        var left = Press(state, ActionIds.TabMoveLeft).State;
        var counted = Press(state, ActionIds.TabMoveLeft, 2).State;
        var right = Press(left, ActionIds.TabMoveRight).State;

        Assert.Equal(["alpha", "gamma", "beta"], left.Workspace.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(["gamma", "alpha", "beta"], counted.Workspace.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(active, counted.Workspace.ActiveTabId);
        Assert.Equal(["alpha", "beta", "gamma"], right.Workspace.Tabs.Select(tab => tab.DisplayName));
    }

    private static AppState Prompted(AppState state)
    {
        var update = AppReducer.Reduce(state, new CreationRequested(CreationKind.Clone));
        var request = update.Effects.OfType<ShowPromptEffect>().Single().Request;

        return AppReducer.Reduce(update.State, new PromptRequestedCommand(request)).State;
    }

    private static AppUpdate Press(AppState state, ActionId action, int? count = null, bool unsent = false) =>
        AppReducer.Reduce(state, new WorkspaceActionPressed(action, count, unsent, unsent, Start));

    private static AppState Empty() => AppState.Empty with
    {
        Shell = ShellState.Empty with { Picker = PickerSession.Empty with { HomeDirectory = PickerFixtures.Home } },
    };

    private static AppState Tabs(params string[] names)
    {
        var state = Empty();
        foreach (var name in names)
        {
            state = AppReducer.Reduce(state, new OpenRepositoryCommand(PickerFixtures.Location("code", name))).State;
        }

        return state;
    }
}
