using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Repositories;

public class WorkingTreeViewReducerTests
{
    [Fact]
    public void GivenANewTab_WhenCreated_ThenItShowsTheHistory()
    {
        Assert.Equal(TabViewMode.History, NewTab().View.Mode);
    }

    [Fact]
    public void GivenUnstagedFiles_WhenOpeningTheWorkingTree_ThenTheViewShowsAndUnstagedHasFocus()
    {
        var state = Load(Snapshot(Unstaged("a.txt"), Staged("b.txt")));

        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());

        Assert.Equal(TabViewMode.WorkingTree, state.View.Mode);
        Assert.Equal(PaneKind.WorkingUnstaged, state.View.FocusedPane);
        Assert.Equal(WorkingTreeSection.Unstaged, state.WorkingTree!.Selection!.Section);
    }

    [Fact]
    public void GivenOnlyStagedFiles_WhenOpeningTheWorkingTree_ThenStagedHasFocus()
    {
        var state = Load(Snapshot(Staged("b.txt")));

        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());

        Assert.Equal(PaneKind.WorkingStaged, state.View.FocusedPane);
        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Staged, "b.txt", 0), state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenStatusNotLoadedYet_WhenOpeningTheWorkingTree_ThenTheViewShowsWithUnstagedFocused()
    {
        var state = RepositoryReducer.Reduce(NewTab(), new OpenWorkingTreeCommand());

        Assert.Equal(TabViewMode.WorkingTree, state.View.Mode);
        Assert.Equal(PaneKind.WorkingUnstaged, state.View.FocusedPane);
        Assert.Null(state.WorkingTree);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenLeaving_ThenTheHistoryShowsWithTheGraphFocusedAndItsSelectionKept()
    {
        var state = RepositoryReducer.Reduce(Load(Snapshot(Unstaged("a.txt"))), new ScrollToCommand(7));
        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());

        state = RepositoryReducer.Reduce(state, new LeaveWorkingTreeCommand());

        Assert.Equal(TabViewMode.History, state.View.Mode);
        Assert.Equal(PaneKind.Graph, state.View.FocusedPane);
        Assert.Equal(7, state.View.ScrollOffset);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenLeavingAndOpeningAgain_ThenTheCursorIsWhereItWas()
    {
        var state = Load(Snapshot(Unstaged("a.txt"), Unstaged("b.txt"), Unstaged("c.txt")));
        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());
        state = RepositoryReducer.Reduce(state, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 2));

        state = RepositoryReducer.Reduce(state, new LeaveWorkingTreeCommand());
        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());

        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "c.txt", 2), state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenTheCursorInUnstaged_WhenFocusingStaged_ThenTheSelectionMovesToTheSameRowOfStaged()
    {
        var state = Load(Snapshot(Unstaged("a.txt"), Unstaged("b.txt"), Staged("s1.txt"), Staged("s2.txt")));
        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());
        state = RepositoryReducer.Reduce(state, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 1));

        state = RepositoryReducer.Reduce(state, new FocusPaneCommand(PaneKind.WorkingStaged));

        Assert.Equal(PaneKind.WorkingStaged, state.View.FocusedPane);
        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Staged, "s2.txt", 1), state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenAnEmptyStagedList_WhenFocusingIt_ThenTheSelectionStays()
    {
        var state = Load(Snapshot(Unstaged("a.txt")));
        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());

        state = RepositoryReducer.Reduce(state, new FocusPaneCommand(PaneKind.WorkingStaged));

        Assert.Equal(PaneKind.WorkingStaged, state.View.FocusedPane);
        Assert.Equal(WorkingTreeSection.Unstaged, state.WorkingTree!.Selection!.Section);
    }

    [Fact]
    public void GivenTheFocusedListRunsEmpty_WhenTheStatusIsReloaded_ThenFocusFollowsTheSelectionToTheOtherList()
    {
        var state = Load(Snapshot(Unstaged("a.txt")));
        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());

        state = RepositoryReducer.Reduce(state, new WorkingTreeLoadedCommand(Snapshot(Staged("a.txt"))));

        Assert.Equal(PaneKind.WorkingStaged, state.View.FocusedPane);
        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Staged, "a.txt", 0), state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenTheStatusBecomesClean_WhenReloaded_ThenTheFocusStaysOnTheList()
    {
        var state = Load(Snapshot(Unstaged("a.txt")));
        state = RepositoryReducer.Reduce(state, new OpenWorkingTreeCommand());

        state = RepositoryReducer.Reduce(state, new WorkingTreeLoadedCommand(Snapshot()));

        Assert.Equal(PaneKind.WorkingUnstaged, state.View.FocusedPane);
        Assert.Null(state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenTheHistoryView_WhenTheStatusReloadsAnEmptyFocusedList_ThenTheFocusIsNotMoved()
    {
        var state = Load(Snapshot(Unstaged("a.txt")));

        state = RepositoryReducer.Reduce(state, new WorkingTreeLoadedCommand(Snapshot(Staged("a.txt"))));

        Assert.Equal(PaneKind.Graph, state.View.FocusedPane);
    }

    [Fact]
    public void GivenConflictsAmongOtherFiles_WhenListed_ThenTheConflictedGroupComesFirstAndEachGroupKeepsGitsOrder()
    {
        var snapshot = Snapshot(
            Unstaged("a.txt"),
            Unstaged("c-conflict.txt") with { Kind = StatusEntryKind.Unmerged, Conflict = ConflictKind.BothModified },
            Unstaged("b.txt"),
            Unstaged("a-conflict.txt") with { Kind = StatusEntryKind.Unmerged, Conflict = ConflictKind.BothAdded });

        var state = Load(snapshot);

        Assert.Equal(["c-conflict.txt", "a-conflict.txt", "a.txt", "b.txt"], state.WorkingTree!.Unstaged.Select(entry => entry.Path));
    }

    [Fact]
    public void GivenConflictsAmongOtherFiles_WhenSelectingByIndex_ThenTheIndexIsTheDisplayedRow()
    {
        var snapshot = Snapshot(Unstaged("a.txt"), Unstaged("z-conflict.txt") with { Kind = StatusEntryKind.Unmerged });
        var state = Load(snapshot);

        state = RepositoryReducer.Reduce(state, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 0));

        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "z-conflict.txt", 0), state.WorkingTree!.Selection);
    }

    private static RepositoryState NewTab()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-working-tree-view-tests");
        var location = new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);

        return RepositoryState.Create(new TabId(1), location);
    }

    private static RepositoryState Load(StatusSnapshot snapshot) =>
        RepositoryReducer.Reduce(NewTab(), new WorkingTreeLoadedCommand(snapshot));

    private static StatusSnapshot Snapshot(params StatusEntry[] entries) =>
        new(new StatusBranch(new HeadState(HeadKind.Branch, "main", null), null, null, null), entries, []);

    private static StatusEntry Unstaged(string path) =>
        new(StatusEntryKind.Ordinary, path, null, FileChangeKind.Unmodified, FileChangeKind.Modified, null, ConflictKind.None, null);

    private static StatusEntry Staged(string path) =>
        new(StatusEntryKind.Ordinary, path, null, FileChangeKind.Modified, FileChangeKind.Unmodified, null, ConflictKind.None, null);
}
