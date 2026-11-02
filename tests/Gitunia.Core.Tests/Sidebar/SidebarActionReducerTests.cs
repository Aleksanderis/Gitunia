using System.Collections.Immutable;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git;
using Xunit;
using static Gitunia.Core.Tests.Sidebar.SidebarFixtures;

namespace Gitunia.Core.Tests.Sidebar;

[Trait("Os", "Independent")]
public class SidebarActionReducerTests
{
    private static readonly ImmutableArray<PaneKind> SidebarAndGraph = [PaneKind.Sidebar, PaneKind.Graph];

    [Theory]
    [InlineData("pane.next", PaneKind.Graph, false, SidebarActionKind.Pane)]
    [InlineData("pane.next", PaneKind.Graph, true, SidebarActionKind.Pane)]
    [InlineData("pane.focus-down", PaneKind.Sidebar, false, SidebarActionKind.Pane)]
    [InlineData("input.accept", PaneKind.Sidebar, true, SidebarActionKind.Filter)]
    [InlineData("cancel", PaneKind.Sidebar, true, SidebarActionKind.Filter)]
    [InlineData("list.down", PaneKind.Sidebar, true, SidebarActionKind.None)]
    [InlineData("list.down", PaneKind.Sidebar, false, SidebarActionKind.Sidebar)]
    [InlineData("list.down", PaneKind.Graph, false, SidebarActionKind.None)]
    [InlineData("sidebar.copy", PaneKind.Sidebar, false, SidebarActionKind.Sidebar)]
    [InlineData("sidebar.copy", PaneKind.Graph, false, SidebarActionKind.None)]
    [InlineData("navigate.back", PaneKind.Sidebar, false, SidebarActionKind.Sidebar)]
    [InlineData("navigate.back", PaneKind.Graph, false, SidebarActionKind.None)]
    [InlineData("graph.jump-to-head", PaneKind.Sidebar, false, SidebarActionKind.None)]
    public void GivenAnActionAndTheFocus_WhenClassifying_ThenItIsTheSidebarsOnlyWhereItShouldBe(string actionName,
        PaneKind focused,
        bool editing,
        SidebarActionKind expected)
    {
        var kind = SidebarActionReducer.Classify(new ActionId(actionName), null, focused, editing);

        Assert.Equal(expected, kind);
    }

    [Fact]
    public void GivenGraphFocus_WhenPressingNextPane_ThenFocusGoesToTheSidebarThenBack()
    {
        var tab = Focused(Tab(), PaneKind.Graph);

        var first = Press(tab, ActionIds.PaneNext);
        Assert.Equal(new FocusPaneCommand(PaneKind.Sidebar), Assert.Single(first.TabCommands));

        var second = Press(Focused(tab, PaneKind.Sidebar), ActionIds.PaneNext);
        Assert.Equal(new FocusPaneCommand(PaneKind.Graph), Assert.Single(second.TabCommands));
    }

    [Fact]
    public void GivenAnOpenDiff_WhenTabbingFromTheDetailPane_ThenTheDiffJoinsTheCycleAfterIt()
    {
        var tab = Focused(Tab(), PaneKind.Detail);
        ImmutableArray<PaneKind> open = [PaneKind.Sidebar, PaneKind.Graph, PaneKind.Detail, PaneKind.Diff];

        var next = Press(tab, ActionIds.PaneNext, open);
        var left = Press(Focused(tab, PaneKind.Diff), ActionIds.PaneFocusLeft, open);

        Assert.Equal(new FocusPaneCommand(PaneKind.Diff), Assert.Single(next.TabCommands));
        Assert.Equal(new FocusPaneCommand(PaneKind.Detail), Assert.Single(left.TabCommands));
    }

    [Fact]
    public void GivenDetailAndDiffBelow_WhenMovingDownAndUp_ThenFocusGoesToTheDetailPaneAndBackToTheGraph()
    {
        ImmutableArray<PaneKind> open = [PaneKind.Sidebar, PaneKind.Graph, PaneKind.Detail, PaneKind.Diff];
        var tab = Focused(Tab(), PaneKind.Graph);

        var down = Press(tab, ActionIds.PaneFocusDown, open);
        var up = Press(Focused(tab, PaneKind.Detail), ActionIds.PaneFocusUp, open);

        Assert.Equal(new FocusPaneCommand(PaneKind.Detail), Assert.Single(down.TabCommands));
        Assert.Equal(new FocusPaneCommand(PaneKind.Graph), Assert.Single(up.TabCommands));
    }

    [Fact]
    public void GivenOnlyTheDiffBelow_WhenMovingDown_ThenTheDiffIsFocused()
    {
        ImmutableArray<PaneKind> open = [PaneKind.Sidebar, PaneKind.Graph, PaneKind.Diff];

        var update = Press(Focused(Tab(), PaneKind.Graph), ActionIds.PaneFocusDown, open);

        Assert.Equal(new FocusPaneCommand(PaneKind.Diff), Assert.Single(update.TabCommands));
    }

    [Fact]
    public void GivenAHiddenSidebar_WhenCyclingPanes_ThenFocusStaysOnTheGraph()
    {
        ImmutableArray<PaneKind> open = [PaneKind.Graph];
        var tab = Focused(Tab(), PaneKind.Graph);

        var next = Press(tab, ActionIds.PaneNext, open);
        var left = Press(tab, ActionIds.PaneFocusLeft, open);

        Assert.Equal(new FocusPaneCommand(PaneKind.Graph), Assert.Single(next.TabCommands));
        Assert.Equal(new FocusPaneCommand(PaneKind.Graph), Assert.Single(left.TabCommands));
    }

    [Fact]
    public void GivenAZoomedPane_WhenTabbing_ThenHiddenPanesAreSkipped()
    {
        ImmutableArray<PaneKind> open = [PaneKind.Diff];

        var update = Press(Focused(Tab(), PaneKind.Diff), ActionIds.PaneNext, open);

        Assert.Equal(new FocusPaneCommand(PaneKind.Diff), Assert.Single(update.TabCommands));
    }

    [Fact]
    public void GivenSidebarFocus_WhenMoving_ThenTheCursorMovesAndAShownStashIsFollowed()
    {
        var tab = OnSidebar(Tab(refs: [Branch("a")]));

        var update = Press(tab, ActionIds.ListDown, count: 2);

        Assert.Equal(new MoveSidebarCursorCommand(ActionIds.ListDown, 2, 10), Assert.Single(update.TabCommands));
        Assert.IsType<FollowStashCursorEffect>(Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenALeafRow_WhenPressingExpand_ThenTheExpandCommandIsTheTabs()
    {
        var update = Press(OnRow(Tab(refs: [Branch("feature")]), "feature"), ActionIds.SidebarExpand);

        Assert.Equal(new ExpandSidebarCommand(), Assert.Single(update.TabCommands));
    }

    [Theory]
    [InlineData("old", "branch name")]
    [InlineData("v2", "tag name")]
    [InlineData("stash@{0}", "stash name")]
    public void GivenAnEntryRow_WhenCopying_ThenItsNameIsCopied(string label, string description)
    {
        var tab = OnRow(
            Tab(refs: [Branch("old"), Tag("v2")], stashes: [StashAt(0, "wip")]),
            label);

        var update = Press(tab, ActionIds.SidebarCopy);

        Assert.Equal(new CopyToClipboardEffect(label, description), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenAWorktreeRow_WhenCopying_ThenItsFullPathIsCopied()
    {
        var tab = OnRow(
            Tab(refs: [Branch("feature")], worktrees: [Worktree("/work/wt-feature", "feature")]),
            "wt-feature");

        var update = Press(tab, ActionIds.SidebarCopy);

        var copy = Assert.IsType<CopyToClipboardEffect>(Assert.Single(update.Effects));
        Assert.Equal("worktree path", copy.Description);
        Assert.Equal("/work/wt-feature", copy.Text);
    }

    [Fact]
    public void GivenASectionRow_WhenCopying_ThenNothingIsCopiedAndTheNoticeSaysWhy()
    {
        var update = Press(OnRow(Tab(refs: [Branch("old")]), "Branches"), ActionIds.SidebarCopy);

        Assert.Equal(SidebarActionReducer.SelectEntryNotice, Notice(update));
    }

    [Fact]
    public void GivenAFilterThatMatchesNothing_WhenCopying_ThenThereIsNothingToCopy()
    {
        var tab = Reduce(OnSidebar(Tab(refs: [Branch("old")])), new SetSidebarFilterCommand("zzz"));

        var update = Press(tab, ActionIds.SidebarCopy);

        Assert.Equal(SidebarActionReducer.NothingToCopyNotice, Notice(update));
    }

    [Fact]
    public void GivenABranchRow_WhenActivating_ThenTheStashViewEndsAndTheCommitIsLookedUp()
    {
        var tab = OnRow(Tab(refs: [Branch("old")]), "old");

        var update = Press(tab, ActionIds.SidebarActivate);

        Assert.Equal(2, update.Effects.Length);
        Assert.IsType<LeaveStashEffect>(update.Effects[0]);
        Assert.True(ObjectId.TryParse(new string('b', 40), out var commit));
        Assert.Equal(new LocateGraphCommitEffect(tab.Id, commit, "old"), update.Effects[1]);
        Assert.Empty(update.TabCommands);
    }

    [Fact]
    public void GivenASectionRow_WhenActivating_ThenItIsFoldedOrUnfolded()
    {
        var update = Press(OnRow(Tab(refs: [Branch("old")]), "Branches"), ActionIds.SidebarActivate);

        Assert.Equal(new ToggleSidebarSectionCommand(), Assert.Single(update.TabCommands));
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAStashRow_WhenActivating_ThenItIsShownInTheDetailPane()
    {
        var tab = OnRow(Tab(stashes: [StashAt(0, "wip")]), "stash@{0}");

        var update = Press(tab, ActionIds.SidebarActivate);

        var show = Assert.IsType<ShowStashEffect>(Assert.Single(update.Effects));
        Assert.Equal("stash@{0}", show.Row.Label);
    }

    [Fact]
    public void GivenTheCommitIsInTheGraph_WhenItIsLocated_ThenTheGraphCursorMovesThere()
    {
        var update = Reduce(OnSidebar(Tab()), new SidebarCommitLocated(Tab().Id, 4));

        Assert.Equal(new SelectGraphIndexEffect(4), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenTheCommitIsMissing_WhenTheGraphIsComplete_ThenTheNoticeSaysItIsNotInTheGraph()
    {
        var update = Reduce(OnSidebar(Tab()), new SidebarCommitMissing(Tab().Id, "feature", true));

        Assert.Equal("feature is not in the graph", Notice(update));
    }

    [Fact]
    public void GivenTheCommitIsMissing_WhenTheGraphIsStillLoading_ThenTheNoticeSaysItIsNotLoadedYet()
    {
        var update = Reduce(OnSidebar(Tab()), new SidebarCommitMissing(Tab().Id, "feature", false));

        Assert.Equal("feature is not loaded yet", Notice(update));
    }

    [Fact]
    public void GivenSidebarFocus_WhenPressingFilter_ThenTheFilterOfThatTabIsBeingTyped()
    {
        var tab = OnSidebar(Tab());

        var update = Press(tab, ActionIds.SidebarFilter);

        Assert.True(update.Session.IsEditing(tab.Id));
        Assert.Empty(update.TabCommands);
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenKeysArrive_ThenTheTextGrowsAndBackspaceRemovesTheLastCharacter()
    {
        var tab = OnSidebar(Tab(refs: [Branch("old")]));
        var session = new SidebarSession(tab.Id);

        var typed = SidebarActionReducer.Reduce(session, new SidebarFilterKeyTyped(tab.Id, new KeyEvent("o")), tab);
        Assert.Equal(new SetSidebarFilterCommand("o"), Assert.Single(typed.TabCommands));

        var withText = Reduce(tab, new SetSidebarFilterCommand("ol"));
        var erased = SidebarActionReducer.Reduce(
            session,
            new SidebarFilterKeyTyped(tab.Id, new KeyEvent(KeyNames.Backspace)),
            withText);
        Assert.Equal(new SetSidebarFilterCommand("o"), Assert.Single(erased.TabCommands));
    }

    [Fact]
    public void GivenASurrogatePairAtTheEnd_WhenBackspacing_ThenBothHalvesGo()
    {
        var tab = Reduce(OnSidebar(Tab()), new SetSidebarFilterCommand("a\U0001F600"));

        var erased = SidebarActionReducer.Reduce(
            new SidebarSession(tab.Id),
            new SidebarFilterKeyTyped(tab.Id, new KeyEvent(KeyNames.Backspace)),
            tab);

        Assert.Equal(new SetSidebarFilterCommand("a"), Assert.Single(erased.TabCommands));
    }

    [Fact]
    public void GivenNoFilterBeingTyped_WhenAKeyArrives_ThenItIsIgnored()
    {
        var tab = OnSidebar(Tab());

        var update = SidebarActionReducer.Reduce(
            SidebarSession.Empty,
            new SidebarFilterKeyTyped(tab.Id, new KeyEvent("x")),
            tab);

        Assert.Empty(update.TabCommands);
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenAccepting_ThenTypingEndsAndTheTextStays()
    {
        var tab = OnSidebar(Tab());

        var update = Press(tab, ActionIds.InputAccept, session: new SidebarSession(tab.Id));

        Assert.Equal(SidebarSession.Empty, update.Session);
        Assert.Empty(update.TabCommands);
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenPressingEscape_ThenTypingEndsAndTheFilterIsCleared()
    {
        var tab = OnSidebar(Tab());

        var update = Press(tab, ActionIds.Cancel, session: new SidebarSession(tab.Id));

        Assert.Equal(SidebarSession.Empty, update.Session);
        Assert.Equal(new SetSidebarFilterCommand(string.Empty), Assert.Single(update.TabCommands));
    }

    [Fact]
    public void GivenAnAcceptedFilter_WhenPressingEscapeTwice_ThenTheFilterClearsFirstThenFocusReturnsToTheGraph()
    {
        var tab = Reduce(OnSidebar(Tab(refs: [Branch("old")])), new SetSidebarFilterCommand("old"));

        var first = Press(tab, ActionIds.Cancel);
        Assert.Equal(new SetSidebarFilterCommand(string.Empty), Assert.Single(first.TabCommands));

        var cleared = Reduce(tab, new SetSidebarFilterCommand(string.Empty));
        var second = Press(cleared, ActionIds.Cancel);
        Assert.Equal(new FocusPaneCommand(PaneKind.Graph), Assert.Single(second.TabCommands));
    }

    [Fact]
    public void GivenSidebarFocus_WhenGoingBack_ThenGraphGetsFocus()
    {
        var update = Press(OnSidebar(Tab()), ActionIds.NavigateBack);

        Assert.Equal(new FocusPaneCommand(PaneKind.Graph), Assert.Single(update.TabCommands));
    }

    [Fact]
    public void GivenGraphFocus_WhenAListMotionArrives_ThenTheReducerChangesNothing()
    {
        var update = Press(Focused(Tab(), PaneKind.Graph), ActionIds.ListDown);

        Assert.Empty(update.TabCommands);
        Assert.Empty(update.Effects);
    }

    private static SidebarActionUpdate Press(
        RepositoryState tab,
        ActionId action,
        ImmutableArray<PaneKind>? open = null,
        int? count = null,
        SidebarSession? session = null)
    {
        var command = new SidebarActionPressed(tab.Id, action, count, open ?? SidebarAndGraph, 10);

        return SidebarActionReducer.Reduce(session ?? SidebarSession.Empty, command, tab);
    }

    private static SidebarActionUpdate Reduce(RepositoryState tab, SidebarActionCommand command) =>
        SidebarActionReducer.Reduce(SidebarSession.Empty, command, tab);

    private static RepositoryState Reduce(RepositoryState tab, SidebarCommand command) =>
        RepositoryReducer.Reduce(tab, command);

    private static RepositoryState Focused(RepositoryState tab, PaneKind pane) =>
        RepositoryReducer.Reduce(tab, new FocusPaneCommand(pane));

    private static RepositoryState OnSidebar(RepositoryState tab) => Focused(tab, PaneKind.Sidebar);

    // The rows are folded by default; a filter unfolds the sections that match and leaves the cursor on the first row.
    private static RepositoryState OnRow(RepositoryState tab, string label)
    {
        var shown = Reduce(OnSidebar(tab), new SetSidebarFilterCommand(label == "Branches" ? "old" : label));
        var rows = SidebarBuilder.Build(shown);
        var index = rows.ToList().FindIndex(row => row.Label == label);

        return Reduce(shown, new SetSidebarCursorCommand(index));
    }

    private static string Notice(SidebarActionUpdate update) =>
        Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text;
}
