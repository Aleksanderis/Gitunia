using System.Collections.Immutable;
using Gitunia.Core.Compare;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests;

/// <summary>The root reducer's cases for the comparison, the quick jump and the sidebar keys (#996).</summary>
public class AppReducerSidebarJumpCompareTests
{
    private static readonly ImmutableArray<PaneKind> Panes = [PaneKind.Sidebar, PaneKind.Graph];

    [Fact]
    public void GivenTheCompareKey_WhenReducing_ThenTheMarkIsSetOnThatTabOnly()
    {
        var state = Opened("alpha", "beta");
        var first = state.Workspace.Tabs[0].Id;
        var second = state.Workspace.Tabs[1].Id;
        var commit = Id('1');

        var update = AppReducer.Reduce(state, new CompareMarkPressed(second, commit));

        Assert.Equal(commit, update.State.Workspace.FindTab(second)!.View.CompareMark);
        Assert.Null(update.State.Workspace.FindTab(first)!.View.CompareMark);
    }

    [Fact]
    public void GivenAClosedTab_WhenACompareCommandArrives_ThenItIsDroppedWithoutEffects()
    {
        var state = Opened("alpha");

        var update = AppReducer.Reduce(state, new CompareStarted(new TabId(99), Id('1')));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAComparisonStarted_WhenReducing_ThenChangesShowOnceAndTheCommitIsRemembered()
    {
        var state = Opened("alpha");
        var tab = state.Workspace.Tabs[0].Id;
        var commit = Id('2');

        var update = AppReducer.Reduce(state, new CompareStarted(tab, commit));

        Assert.Equal(BottomTab.Changes, update.State.Workspace.FindTab(tab)!.View.BottomTab);
        Assert.Equal(commit, update.State.Shell.Compare.Of(tab).Followed);
    }

    [Fact]
    public void GivenTheMarkIsCleared_WhenReducing_ThenTheTabForgetsWhatItFollowed()
    {
        var state = Opened("alpha");
        var tab = state.Workspace.Tabs[0].Id;
        var commit = Id('2');
        state = AppReducer.Reduce(state, new CompareMarkPressed(tab, commit)).State;
        state = AppReducer.Reduce(state, new CompareStarted(tab, Id('3'))).State;

        var update = AppReducer.Reduce(state, new CompareEscapePressed(tab));

        Assert.Null(update.State.Workspace.FindTab(tab)!.View.CompareMark);
        Assert.Null(update.State.Shell.Compare.Of(tab).Followed);
    }

    [Fact]
    public void GivenTheJumpOpens_WhenReducing_ThenInsertModeIsOnAndClosingReturnsToNormal()
    {
        var state = Opened("alpha");
        var tab = state.Workspace.Tabs[0].Id;

        var opened = AppReducer.Reduce(state, new QuickJumpOpenRequested(tab, PaneKind.Graph, true, null)).State;

        Assert.Equal(KeyMode.Insert, opened.Shell.Mode);
        Assert.Equal(new QuickJumpSession(tab, PaneKind.Graph, string.Empty), opened.Shell.QuickJump);

        var closed = AppReducer.Reduce(opened, new QuickJumpClosed()).State;

        Assert.Equal(KeyMode.Normal, closed.Shell.Mode);
        Assert.Null(closed.Shell.QuickJump);
    }

    [Fact]
    public void GivenAJumpThatCannotOpen_WhenReducing_ThenTheModeStaysNormal()
    {
        var state = Opened("alpha");

        var update = AppReducer.Reduce(state, new QuickJumpOpenRequested(null, PaneKind.Graph, false, null));

        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Equal(state.Shell.ModeChanges, update.State.Shell.ModeChanges);
        Assert.Single(update.Effects);
    }

    [Fact]
    public void GivenALabelKeyInTheJump_WhenReducing_ThenTheModeReturnsToNormalAndTheRowIsJumpedTo()
    {
        var state = Opened("alpha");
        var tab = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new QuickJumpOpenRequested(tab, PaneKind.Graph, true, null)).State;
        ImmutableArray<JumpLabel> labels = [new(7, 0, 1, 'q')];
        state = AppReducer.Reduce(state, new QuickJumpKeyPressed(new KeyEvent("f"), labels)).State;

        var update = AppReducer.Reduce(state, new QuickJumpKeyPressed(new KeyEvent("q"), labels));

        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Equal(new JumpToRowEffect(7), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenTheFilterKey_WhenReducing_ThenInsertModeIsOnUntilItIsAccepted()
    {
        var (state, tab) = FocusedSidebar();

        var typing = AppReducer.Reduce(state, Pressed(tab, ActionIds.SidebarFilter)).State;

        Assert.Equal(KeyMode.Insert, typing.Shell.Mode);
        Assert.True(typing.Shell.Sidebar.IsEditing(tab));

        var accepted = AppReducer.Reduce(typing, Pressed(tab, ActionIds.InputAccept)).State;

        Assert.Equal(KeyMode.Normal, accepted.Shell.Mode);
        Assert.Null(accepted.Shell.Sidebar.FilterTab);
    }

    [Fact]
    public void GivenTypedKeys_WhenReducing_ThenTheTabsFilterTextFollows()
    {
        var (state, tab) = FocusedSidebar();
        state = AppReducer.Reduce(state, Pressed(tab, ActionIds.SidebarFilter)).State;

        state = AppReducer.Reduce(state, new SidebarFilterKeyTyped(tab, new KeyEvent("o"))).State;
        state = AppReducer.Reduce(state, new SidebarFilterKeyTyped(tab, new KeyEvent("l"))).State;

        Assert.Equal("ol", state.Workspace.FindTab(tab)!.Sidebar.FilterText);
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenAnotherTabBecomesActive_ThenTheFilterEndsAndNormalModeReturns()
    {
        var (state, tab) = FocusedSidebar();
        state = AppReducer.Reduce(state, Pressed(tab, ActionIds.SidebarFilter)).State;

        var other = AppReducer.Reduce(state, new OpenRepositoryCommand(Location("beta"))).State;

        Assert.NotEqual(tab, other.Workspace.ActiveTabId);
        Assert.Equal(KeyMode.Normal, other.Shell.Mode);
        Assert.Null(other.Shell.Sidebar.FilterTab);
    }

    [Fact]
    public void GivenASidebarCommandForAClosedTab_WhenReducing_ThenItIsDropped()
    {
        var state = Opened("alpha");

        var update = AppReducer.Reduce(state, new SidebarCommitLocated(new TabId(99), 3));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    private static SidebarActionPressed Pressed(TabId tab, ActionId action) =>
        new(tab, action, null, Panes, 10);

    private static (AppState State, TabId Tab) FocusedSidebar()
    {
        var state = Opened("alpha");
        var tab = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new TabCommand(tab, new FocusPaneCommand(PaneKind.Sidebar))).State;

        return (state, tab);
    }

    private static AppState Opened(params string[] names)
    {
        var state = AppState.Empty;
        foreach (var name in names)
        {
            state = AppReducer.Reduce(state, new OpenRepositoryCommand(Location(name))).State;
        }

        return state;
    }

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-core-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private static ObjectId Id(char digit)
    {
        Assert.True(ObjectId.TryParse(new string(digit, 40), out var id));

        return id;
    }
}
