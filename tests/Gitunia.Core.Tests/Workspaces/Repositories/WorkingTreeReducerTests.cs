using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Repositories;

public class WorkingTreeReducerTests
{
    [Fact]
    public void GivenNewTab_WhenCreated_ThenNoWorkingTreeYet()
    {
        Assert.Null(NewTab().WorkingTree);
    }

    [Fact]
    public void GivenSnapshot_WhenLoaded_ThenKeptAndSummaryUpdated()
    {
        var snapshot = Snapshot(Unstaged("a.txt"), Staged("b.txt"));

        var state = RepositoryReducer.Reduce(NewTab(), new WorkingTreeLoadedCommand(snapshot));

        Assert.Same(snapshot, state.WorkingTree!.Snapshot);
        Assert.Equal(new RepositoryStatusSummary(true, 0, 0), state.Status);
        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "a.txt", 0), state.WorkingTree.Selection);
    }

    [Fact]
    public void GivenLoadedSnapshot_WhenNewContentLoaded_ThenReplaced()
    {
        var first = RepositoryReducer.Reduce(NewTab(), new WorkingTreeLoadedCommand(Snapshot(Unstaged("a.txt"))));
        var second = Snapshot(Unstaged("a.txt"), Unstaged("c.txt"));

        var state = RepositoryReducer.Reduce(first, new WorkingTreeLoadedCommand(second));

        Assert.Same(second, state.WorkingTree!.Snapshot);
    }

    [Fact]
    public void GivenSameContentInNewInstance_WhenLoaded_ThenStateIsUnchanged()
    {
        var first = RepositoryReducer.Reduce(NewTab(), new WorkingTreeLoadedCommand(Snapshot(Unstaged("a.txt"), Staged("b.txt"))));

        var state = RepositoryReducer.Reduce(first, new WorkingTreeLoadedCommand(Snapshot(Unstaged("a.txt"), Staged("b.txt"))));

        Assert.Same(first, state);
    }

    [Fact]
    public void GivenSelectedEntryStillPresent_WhenRefreshMovesIt_ThenSelectionFollowsThePath()
    {
        var state = Load(Snapshot(Unstaged("a.txt"), Unstaged("b.txt"), Unstaged("c.txt")));
        state = RepositoryReducer.Reduce(state, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 2));

        state = RepositoryReducer.Reduce(state, new WorkingTreeLoadedCommand(Snapshot(Unstaged("a.txt"), Unstaged("c.txt"))));

        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "c.txt", 1), state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenSelectedEntryStaged_WhenRefreshed_ThenCursorStaysAtTheSameIndexInTheList()
    {
        var state = Load(Snapshot(Unstaged("a.txt"), Unstaged("b.txt")));
        state = RepositoryReducer.Reduce(state, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 1));

        state = RepositoryReducer.Reduce(state, new WorkingTreeLoadedCommand(Snapshot(Unstaged("a.txt"), Staged("b.txt"))));

        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "a.txt", 0), state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenSelectedEntryGone_WhenRefreshed_ThenSelectionLandsNearTheOldIndex()
    {
        var state = Load(Snapshot(Unstaged("a.txt"), Unstaged("b.txt"), Unstaged("c.txt")));
        state = RepositoryReducer.Reduce(state, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 2));

        state = RepositoryReducer.Reduce(state, new WorkingTreeLoadedCommand(Snapshot(Unstaged("a.txt"), Unstaged("b.txt"))));

        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "b.txt", 1), state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenStatusBecomesClean_WhenRefreshed_ThenNothingIsSelected()
    {
        var state = Load(Snapshot(Unstaged("a.txt")));

        state = RepositoryReducer.Reduce(state, new WorkingTreeLoadedCommand(Snapshot()));

        Assert.Null(state.WorkingTree!.Selection);
        Assert.False(state.Status!.IsDirty);
    }

    [Fact]
    public void GivenUntrackedAndIgnored_WhenListed_ThenIgnoredIsHidden()
    {
        var untracked = new StatusEntry(StatusEntryKind.Untracked, "u.txt", null, FileChangeKind.Unmodified, FileChangeKind.Unmodified, null, ConflictKind.None, null);
        var ignored = untracked with { Kind = StatusEntryKind.Ignored, Path = "i.txt" };

        var state = Load(Snapshot(untracked, ignored));

        Assert.Equal(["u.txt"], state.WorkingTree!.Unstaged.Select(entry => entry.Path));
        Assert.Empty(state.WorkingTree.Staged);
    }

    [Fact]
    public void GivenOutOfRangeIndex_WhenSelecting_ThenClamped()
    {
        var state = Load(Snapshot(Unstaged("a.txt"), Unstaged("b.txt")));

        state = RepositoryReducer.Reduce(state, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 99));

        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "b.txt", 1), state.WorkingTree!.Selection);
    }

    [Fact]
    public void GivenEmptySection_WhenSelecting_ThenIgnored()
    {
        var state = Load(Snapshot(Unstaged("a.txt")));

        var after = RepositoryReducer.Reduce(state, new SelectWorkingTreeFileCommand(WorkingTreeSection.Staged, 0));

        Assert.Same(state, after);
    }

    [Fact]
    public void GivenFullSnapshot_WhenSummaryOnlyLoaded_ThenSnapshotIsKept()
    {
        var state = Load(Snapshot(Unstaged("a.txt")));

        state = RepositoryReducer.Reduce(state, new StatusLoadedCommand(new RepositoryStatusSummary(true, 1, 0)));

        Assert.NotNull(state.WorkingTree);
        Assert.Equal(1, state.Status!.Ahead);
    }

    [Fact]
    public void GivenSameEntriesButNewDivergence_WhenLoaded_ThenSnapshotAndSummaryUpdate()
    {
        var state = Load(Snapshot(Unstaged("a.txt")));
        var branch = state.WorkingTree!.Snapshot.Branch with { Ahead = 2, Behind = 1 };
        var diverged = Snapshot(Unstaged("a.txt")) with { Branch = branch };

        state = RepositoryReducer.Reduce(state, new WorkingTreeLoadedCommand(diverged));

        Assert.Same(diverged, state.WorkingTree!.Snapshot);
        Assert.Equal(new RepositoryStatusSummary(true, 2, 1), state.Status);
    }

    [Fact]
    public void GivenUnmergedEntry_WhenListed_ThenInUnstaged()
    {
        var conflict = Unstaged("c.txt") with { Kind = StatusEntryKind.Unmerged };

        var state = Load(Snapshot(conflict));

        Assert.Equal(["c.txt"], state.WorkingTree!.Unstaged.Select(entry => entry.Path));
    }

    [Fact]
    public void GivenPathChangedInIndexAndWorktree_WhenListed_ThenInBothLists()
    {
        var both = Unstaged("m.txt") with { IndexChange = FileChangeKind.Modified };

        var state = Load(Snapshot(both));

        Assert.Single(state.WorkingTree!.Unstaged);
        Assert.Single(state.WorkingTree.Staged);
    }

    private static RepositoryState NewTab()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-working-tree-tests");
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
