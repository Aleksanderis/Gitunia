using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces;

public class WorkspaceReducerTests
{
    [Fact]
    public void GivenEmptyWorkspace_WhenOpeningRepository_ThenTabIsActiveAndLoadEffectCarriesPath()
    {
        var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(Location("alpha")));

        var tab = Assert.Single(update.Workspace.Tabs);
        Assert.Equal(tab.Id, update.Workspace.ActiveTabId);
        Assert.Equal("alpha", tab.DisplayName);
        var effect = Assert.IsType<LoadRepositoryEffect>(Assert.Single(update.Effects));
        Assert.Equal(tab.Id, effect.TabId);
        Assert.Equal(Location("alpha").CommandPath, effect.RepositoryPath);
    }

    [Fact]
    public void GivenATab_WhenRenaming_ThenTheTitleChangesAndABlankNameRestoresTheRepositoryName()
    {
        var workspace = Open(Workspace.Empty, "alpha");
        var id = workspace.Tabs[0].Id;

        var renamed = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new SetTabNameCommand("  Billing "))).Workspace;
        var restored = WorkspaceReducer.Reduce(renamed, new TabCommand(id, new SetTabNameCommand("  "))).Workspace;

        Assert.Equal("Billing", renamed.Tabs[0].Title);
        Assert.Equal("alpha", renamed.Tabs[0].DisplayName);
        Assert.Equal("alpha", restored.Tabs[0].Title);
        Assert.Null(restored.Tabs[0].CustomName);
    }

    [Fact]
    public void GivenOpenRepository_WhenOpeningItAgain_ThenItsTabIsFocusedWithoutDuplicateOrEffect()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");

        var update = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(Location("alpha")));

        Assert.Equal(2, update.Workspace.Tabs.Length);
        Assert.Equal(IdOf(workspace, "alpha"), update.Workspace.ActiveTabId);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenOpenRepository_WhenReopeningWithTrailingSeparator_ThenItIsStillDeduped()
    {
        var workspace = Open(Workspace.Empty, "alpha");
        var root = Location("alpha").RootPath + Path.DirectorySeparatorChar;
        var location = Location("alpha") with { WorkingTreePath = root };

        var update = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(location));

        Assert.Single(update.Workspace.Tabs);
    }

    [Fact]
    public void GivenBareRepository_WhenOpeningTwice_ThenDedupedByGitDirectory()
    {
        var gitDirectory = Path.Combine(Path.GetTempPath(), "bare.git");
        var bare = new RepositoryLocation(null, gitDirectory, gitDirectory, true, false);

        var workspace = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(bare)).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(bare)).Workspace;

        Assert.Single(workspace.Tabs);
    }

    [Fact]
    public void GivenCaseVariants_WhenComparingPaths_ThenCaseSensitivityFollowsTheComparer()
    {
        Assert.True(RepositoryPathComparer.Create(ignoreCase: true).Equals("/a/Repo", "/a/repo/"));
        Assert.False(RepositoryPathComparer.Create(ignoreCase: false).Equals("/a/Repo", "/a/repo"));
    }

    [Fact]
    public void GivenThreeTabs_WhenOpening_ThenIdsAreUniqueAndOrderIsKept()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "gamma");

        Assert.Equal(["alpha", "beta", "gamma"], workspace.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(3, workspace.Tabs.Select(tab => tab.Id).Distinct().Count());
    }

    [Fact]
    public void GivenActiveTab_WhenClosingIt_ThenRightNeighbourBecomesActive()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "gamma");
        workspace = WorkspaceReducer.Reduce(workspace, new GoToTabCommand(2)).Workspace;

        var closed = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(IdOf(workspace, "beta"))).Workspace;

        Assert.Equal(["alpha", "gamma"], closed.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(IdOf(workspace, "gamma"), closed.ActiveTabId);
    }

    [Fact]
    public void GivenLastTabActive_WhenClosingIt_ThenLeftNeighbourBecomesActive()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");

        var closed = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(IdOf(workspace, "beta"))).Workspace;

        Assert.Equal(IdOf(workspace, "alpha"), closed.ActiveTabId);
    }

    [Fact]
    public void GivenSingleTab_WhenClosingIt_ThenWorkspaceIsEmptyButKeepsRecents()
    {
        var workspace = Open(Workspace.Empty, "alpha");

        var closed = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(IdOf(workspace, "alpha"))).Workspace;

        Assert.Empty(closed.Tabs);
        Assert.Null(closed.ActiveTabId);
        Assert.Single(closed.Recents);
    }

    [Fact]
    public void GivenTab_WhenClosingIt_ThenItsLoadIsCancelled()
    {
        var workspace = Open(Workspace.Empty, "alpha");
        var id = IdOf(workspace, "alpha");

        var update = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(id));

        Assert.Equal(new CancelLoadEffect(id), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenMixedSeparators_WhenComparingPaths_ThenTheyMatch()
    {
        var comparer = RepositoryPathComparer.Create(ignoreCase: false);

        Assert.True(comparer.Equals(
            $"a{Path.AltDirectorySeparatorChar}repo",
            $"a{Path.DirectorySeparatorChar}repo"));
    }

    [Fact]
    public void GivenInactiveTab_WhenClosingIt_ThenActiveTabIsUnchanged()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");

        var closed = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(IdOf(workspace, "alpha"))).Workspace;

        Assert.Equal(IdOf(workspace, "beta"), closed.ActiveTabId);
    }

    [Fact]
    public void GivenClosedTabId_WhenReopeningRepository_ThenItGetsANewId()
    {
        var workspace = Open(Workspace.Empty, "alpha");
        var oldId = IdOf(workspace, "alpha");
        workspace = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(oldId)).Workspace;

        workspace = Open(workspace, "alpha");

        Assert.NotEqual(oldId, IdOf(workspace, "alpha"));
    }

    [Fact]
    public void GivenTabs_WhenCyclingNextAndPrevious_ThenItWraps()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");

        var next = WorkspaceReducer.Reduce(workspace, new NextTabCommand()).Workspace;
        var previous = WorkspaceReducer.Reduce(workspace, new PreviousTabCommand()).Workspace;

        Assert.Equal(IdOf(workspace, "alpha"), next.ActiveTabId);
        Assert.Equal(IdOf(workspace, "alpha"), previous.ActiveTabId);
    }

    [Fact]
    public void GivenEmptyWorkspace_WhenCyclingOrJumping_ThenNothingHappens()
    {
        foreach (var command in new WorkspaceCommand[] { new NextTabCommand(), new PreviousTabCommand(), new GoToTabCommand(1), new ActivatePreviousTabCommand() })
        {
            var update = WorkspaceReducer.Reduce(Workspace.Empty, command);

            Assert.Equal(Workspace.Empty, update.Workspace);
        }
    }

    [Fact]
    public void GivenTabs_WhenJumpingToPosition_ThenOnlyValidOneBasedPositionsSwitch()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "gamma");

        var first = WorkspaceReducer.Reduce(workspace, new GoToTabCommand(1)).Workspace;
        var outOfRange = WorkspaceReducer.Reduce(first, new GoToTabCommand(9)).Workspace;
        var zero = WorkspaceReducer.Reduce(first, new GoToTabCommand(0)).Workspace;

        Assert.Equal(IdOf(workspace, "alpha"), first.ActiveTabId);
        Assert.Equal(first, outOfRange);
        Assert.Equal(first, zero);
    }

    [Fact]
    public void GivenSwitchedTabs_WhenActivatingPrevious_ThenItTogglesBetweenTheTwo()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "gamma");
        workspace = WorkspaceReducer.Reduce(workspace, new GoToTabCommand(1)).Workspace;

        var back = WorkspaceReducer.Reduce(workspace, new ActivatePreviousTabCommand()).Workspace;
        var again = WorkspaceReducer.Reduce(back, new ActivatePreviousTabCommand()).Workspace;

        Assert.Equal(IdOf(workspace, "gamma"), back.ActiveTabId);
        Assert.Equal(IdOf(workspace, "alpha"), again.ActiveTabId);
    }

    [Fact]
    public void GivenPreviousTab_WhenItIsClosed_ThenPreviousIsCleared()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");

        var closed = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(IdOf(workspace, "alpha"))).Workspace;

        Assert.Null(closed.PreviousTabId);
    }

    [Fact]
    public void GivenUnknownTabId_WhenActivating_ThenWorkspaceIsUnchanged()
    {
        var workspace = Open(Workspace.Empty, "alpha");

        var update = WorkspaceReducer.Reduce(workspace, new ActivateTabCommand(new TabId(999)));

        Assert.Equal(workspace, update.Workspace);
    }

    [Fact]
    public void GivenTwoTabs_WhenRoutingCommandToOne_ThenOnlyThatTabChanges()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");
        var alphaId = IdOf(workspace, "alpha");
        var betaBefore = workspace.FindTab(IdOf(workspace, "beta"));

        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(alphaId, new SetFilterCommand("fix"))).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(alphaId, new ScrollToCommand(40))).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(alphaId, new FocusPaneCommand(PaneKind.Sidebar))).Workspace;

        var alpha = workspace.FindTab(alphaId)!;
        Assert.Equal(new RepositoryViewState(null, 40, "fix", PaneKind.Sidebar), alpha.View);
        Assert.Equal(betaBefore, workspace.FindTab(IdOf(workspace, "beta")));
    }

    [Fact]
    public void GivenTwoTabs_WhenInactiveTabReceivesCommand_ThenActiveTabIsUnchanged()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");
        var activeBefore = workspace.ActiveTabId;

        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(IdOf(workspace, "alpha"), new SetFilterCommand("x"))).Workspace;

        Assert.Equal(activeBefore, workspace.ActiveTabId);
    }

    [Fact]
    public void GivenClosedTab_WhenLateResultArrives_ThenItIsDropped()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");
        var alphaId = IdOf(workspace, "alpha");
        workspace = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(alphaId)).Workspace;

        var update = WorkspaceReducer.Reduce(workspace, new TabCommand(alphaId, new HeadLoadedCommand(new HeadState(HeadKind.Branch, "main", "abc"))));

        Assert.Equal(workspace, update.Workspace);
    }

    [Fact]
    public void GivenTab_WhenLoadResultsArrive_ThenHeadRefsAndMissingFlagAreStored()
    {
        var workspace = Open(Workspace.Empty, "alpha");
        var id = IdOf(workspace, "alpha");
        var head = new HeadState(HeadKind.Branch, "main", "abc");

        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new HeadLoadedCommand(head))).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new RefsLoadedCommand([]))).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new SetMissingCommand(true))).Workspace;

        var tab = workspace.FindTab(id)!;
        Assert.Equal(head, tab.Head);
        Assert.True(tab.IsMissing);
    }

    [Fact]
    public void GivenNegativeOffset_WhenScrolling_ThenItClampsToZero()
    {
        var workspace = Open(Workspace.Empty, "alpha");
        var id = IdOf(workspace, "alpha");

        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new ScrollToCommand(-5))).Workspace;

        Assert.Equal(0, workspace.FindTab(id)!.View.ScrollOffset);
    }

    [Fact]
    public void GivenRecents_WhenReopeningOlderRepository_ThenItMovesToTheFront()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "alpha");

        Assert.Equal([Location("alpha").RootPath, Location("beta").RootPath], workspace.Recents);
    }

    [Fact]
    public void GivenMoreThanTheLimit_WhenOpening_ThenOldestRecentsAreDropped()
    {
        var names = Enumerable.Range(0, Workspace.MaximumRecents + 5).Select(number => $"repo{number}").ToArray();

        var workspace = Open(Workspace.Empty, names);

        Assert.Equal(Workspace.MaximumRecents, workspace.Recents.Length);
        Assert.Equal(Location(names[^1]).RootPath, workspace.Recents[0]);
    }

    [Fact]
    public void GivenRecents_WhenRemoving_ThenOnlyTheListEntryGoesAndTabsStay()
    {
        var workspace = Open(Workspace.Empty, "alpha");

        workspace = WorkspaceReducer.Reduce(workspace, new RemoveRecentCommand(Location("alpha").RootPath)).Workspace;

        Assert.Empty(workspace.Recents);
        Assert.Single(workspace.Tabs);
    }

    [Fact]
    public void GivenRepository_WhenTogglingPinTwice_ThenItIsPinnedThenUnpinned()
    {
        var path = Location("alpha").RootPath;

        var pinned = WorkspaceReducer.Reduce(Workspace.Empty, new TogglePinCommand(path)).Workspace;
        var unpinned = WorkspaceReducer.Reduce(pinned, new TogglePinCommand(path)).Workspace;

        Assert.True(pinned.IsPinned(path));
        Assert.False(unpinned.IsPinned(path));
    }

    [Fact]
    public void GivenOpenedRepository_WhenReducing_ThenTheEmptyWorkspaceIsNotMutated()
    {
        var opened = Open(Workspace.Empty, "alpha");

        Assert.Single(opened.Tabs);
        Assert.Empty(Workspace.Empty.Tabs);
        Assert.Null(Workspace.Empty.ActiveTab);
    }

    [Fact]
    public void GivenATab_WhenSelectingABottomTab_ThenOnlyThatTabsViewChanges()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");
        var alpha = IdOf(workspace, "alpha");

        var update = WorkspaceReducer.Reduce(workspace, new TabCommand(alpha, new SetBottomTabCommand(BottomTab.Files))).Workspace;

        Assert.Equal(BottomTab.Files, update.Tabs.Single(tab => tab.Id == alpha).View.BottomTab);
        Assert.Equal(BottomTab.Changes, update.Tabs.Single(tab => tab.Id != alpha).View.BottomTab);
    }

    [Fact]
    public void GivenATab_WhenTogglingThePathStyle_ThenOnlyThatTabCyclesNameFirstFullPathAndTree()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");
        var alpha = IdOf(workspace, "alpha");
        Assert.Equal(FilePathStyle.NameFirst, workspace.Tabs.Single(tab => tab.Id == alpha).View.PathStyle);

        var toggled = WorkspaceReducer.Reduce(workspace, new TabCommand(alpha, new TogglePathStyleCommand())).Workspace;
        var tree = WorkspaceReducer.Reduce(toggled, new TabCommand(alpha, new TogglePathStyleCommand())).Workspace;
        var back = WorkspaceReducer.Reduce(tree, new TabCommand(alpha, new TogglePathStyleCommand())).Workspace;

        Assert.Equal(FilePathStyle.FullPath, toggled.Tabs.Single(tab => tab.Id == alpha).View.PathStyle);
        Assert.Equal(FilePathStyle.Tree, tree.Tabs.Single(tab => tab.Id == alpha).View.PathStyle);
        Assert.Equal(FilePathStyle.NameFirst, toggled.Tabs.Single(tab => tab.Id != alpha).View.PathStyle);
        Assert.Equal(FilePathStyle.NameFirst, back.Tabs.Single(tab => tab.Id == alpha).View.PathStyle);
    }

    [Fact]
    public void GivenATab_WhenMarkingACommit_ThenOnlyThatTabHoldsTheMarkUntilItIsCleared()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta");
        var alpha = IdOf(workspace, "alpha");
        Assert.True(ObjectId.TryParse(new string('a', 40), out var commit));

        var mark = new TabCommand(alpha, new SetCompareMarkCommand(commit));
        var clear = new TabCommand(alpha, new SetCompareMarkCommand(null));
        var marked = WorkspaceReducer.Reduce(workspace, mark).Workspace;
        var cleared = WorkspaceReducer.Reduce(marked, clear).Workspace;

        Assert.Equal(commit, marked.Tabs.Single(tab => tab.Id == alpha).View.CompareMark);
        Assert.Null(marked.Tabs.Single(tab => tab.Id != alpha).View.CompareMark);
        Assert.Null(cleared.Tabs.Single(tab => tab.Id == alpha).View.CompareMark);
    }

    [Fact]
    public void GivenTabWithViewState_WhenSwitchingWorktree_ThenLocationChangesViewStaysAndOldStatusGoes()
    {
        var workspace = Open(Workspace.Empty, "alpha");
        var id = workspace.Tabs[0].Id;
        Assert.True(ObjectId.TryParse(new string('a', 40), out var commit));
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new SelectCommitCommand(commit))).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new ScrollToCommand(7))).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new SetFilterCommand("fix"))).Workspace;
        var summary = new RepositoryStatusSummary(true, 1, 0);
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new StatusLoadedCommand(summary))).Workspace;

        var update = WorkspaceReducer.Reduce(workspace, new SwitchWorktreeCommand(id, Location("alpha-wt")));

        var tab = Assert.Single(update.Workspace.Tabs);
        Assert.Equal(Location("alpha-wt").RootPath, tab.Location.RootPath);
        Assert.Equal(commit, tab.View.SelectedCommit);
        Assert.Equal(7, tab.View.ScrollOffset);
        Assert.Equal("fix", tab.View.FilterText);
        Assert.Null(tab.Status);
        Assert.Null(tab.Head);
        Assert.Null(tab.WorkingTree);
        Assert.Equal(id, update.Workspace.ActiveTabId);
        var effect = Assert.IsType<LoadRepositoryEffect>(Assert.Single(update.Effects));
        Assert.Equal(id, effect.TabId);
        Assert.Equal(Location("alpha-wt").CommandPath, effect.RepositoryPath);
        Assert.Equal(Location("alpha-wt").RootPath, update.Workspace.Recents[0]);
    }

    [Fact]
    public void GivenAnotherTabOnTheWorktree_WhenSwitchingToIt_ThenNothingChanges()
    {
        var workspace = Open(Workspace.Empty, "alpha", "alpha-wt");

        var command = new SwitchWorktreeCommand(IdOf(workspace, "alpha"), Location("alpha-wt"));

        var update = WorkspaceReducer.Reduce(workspace, command);

        Assert.Same(workspace, update.Workspace);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTabOnTheWorktree_WhenSwitchingToTheSamePath_ThenNothingChanges()
    {
        var workspace = Open(Workspace.Empty, "alpha");

        var command = new SwitchWorktreeCommand(IdOf(workspace, "alpha"), Location("alpha"));

        var update = WorkspaceReducer.Reduce(workspace, command);

        Assert.Same(workspace, update.Workspace);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenClosedTab_WhenSwitching_ThenNothingChanges()
    {
        var workspace = Open(Workspace.Empty, "alpha");

        var update = WorkspaceReducer.Reduce(workspace, new SwitchWorktreeCommand(new TabId(99), Location("alpha-wt")));

        Assert.Same(workspace, update.Workspace);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenThreeTabs_WhenMovingTheActiveTabRightAndLeft_ThenItSwapsAndStaysActive()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "gamma");
        workspace = WorkspaceReducer.Reduce(workspace, new ActivateTabCommand(IdOf(workspace, "alpha"))).Workspace;

        var right = WorkspaceReducer.Reduce(workspace, new MoveTabCommand(1)).Workspace;
        var back = WorkspaceReducer.Reduce(right, new MoveTabCommand(-1)).Workspace;

        Assert.Equal(["beta", "alpha", "gamma"], right.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(IdOf(workspace, "alpha"), right.ActiveTabId);
        Assert.Equal(["alpha", "beta", "gamma"], back.Tabs.Select(tab => tab.DisplayName));
    }

    [Fact]
    public void GivenTheActiveTabAtAnEnd_WhenMovingPastIt_ThenNothingChangesAndThereIsNoWrap()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "gamma");

        var past = WorkspaceReducer.Reduce(workspace, new MoveTabCommand(1)).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new ActivateTabCommand(IdOf(workspace, "alpha"))).Workspace;
        var before = WorkspaceReducer.Reduce(workspace, new MoveTabCommand(-1)).Workspace;

        Assert.Equal(["alpha", "beta", "gamma"], before.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(["alpha", "beta", "gamma"], past.Tabs.Select(tab => tab.DisplayName));
    }

    [Fact]
    public void GivenACount_WhenMovingTheActiveTab_ThenItMovesThatFarAndStopsAtTheEnd()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "gamma", "delta");
        workspace = WorkspaceReducer.Reduce(workspace, new ActivateTabCommand(IdOf(workspace, "alpha"))).Workspace;

        var two = WorkspaceReducer.Reduce(workspace, new MoveTabCommand(2)).Workspace;
        var clamped = WorkspaceReducer.Reduce(workspace, new MoveTabCommand(9)).Workspace;

        Assert.Equal(["beta", "gamma", "alpha", "delta"], two.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(["beta", "gamma", "delta", "alpha"], clamped.Tabs.Select(tab => tab.DisplayName));
    }

    [Fact]
    public void GivenAPreviousTab_WhenTabsAreMoved_ThenThePreviousTabIsTheSameTabAndNumbersFollowTheOrder()
    {
        var workspace = Open(Workspace.Empty, "alpha", "beta", "gamma");
        workspace = WorkspaceReducer.Reduce(workspace, new ActivateTabCommand(IdOf(workspace, "alpha"))).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new ActivateTabCommand(IdOf(workspace, "gamma"))).Workspace;

        var moved = WorkspaceReducer.Reduce(workspace, new MoveTabCommand(-2)).Workspace;
        var back = WorkspaceReducer.Reduce(moved, new ActivatePreviousTabCommand()).Workspace;
        var third = WorkspaceReducer.Reduce(moved, new GoToTabCommand(3)).Workspace;

        Assert.Equal(["gamma", "alpha", "beta"], moved.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(IdOf(workspace, "alpha"), back.ActiveTabId);
        Assert.Equal(IdOf(workspace, "beta"), third.ActiveTabId);
    }

    [Fact]
    public void GivenNoTabs_WhenMoving_ThenNothingHappens()
    {
        var moved = WorkspaceReducer.Reduce(Workspace.Empty, new MoveTabCommand(1)).Workspace;

        Assert.Same(Workspace.Empty, moved);
    }

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-workspace-tests", name);
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private static Workspace Open(Workspace workspace, params string[] names)
    {
        foreach (var name in names)
        {
            workspace = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(Location(name))).Workspace;
        }

        return workspace;
    }

    private static TabId IdOf(Workspace workspace, string name) => workspace.FindTabByRootPath(Location(name).RootPath)!.Id;
}
