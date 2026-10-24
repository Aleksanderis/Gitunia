using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.Conflicts;
using Xunit;

namespace Gitunia.Tui.Tests.App.Conflicts;

[Trait("Os", "Independent")]
public class ConflictedSelectionTests
{
    [Fact]
    public void GivenTheCursorOnAConflictedFile_WhenFinding_ThenTheEntryIsReturned()
    {
        var tree = Tree(Selection("a.txt"), Entry(StatusEntryKind.Unmerged, "a.txt"));

        Assert.Equal("a.txt", ConflictedSelection.Find(tree)?.Path);
    }

    [Fact]
    public void GivenTheCursorOnAPlainModifiedFile_WhenFinding_ThenNothingIsReturned()
    {
        var tree = Tree(Selection("a.txt"), Entry(StatusEntryKind.Ordinary, "a.txt"));

        Assert.Null(ConflictedSelection.Find(tree));
    }

    [Fact]
    public void GivenTheCursorOnAnotherFileThanTheConflictedOne_WhenFinding_ThenNothingIsReturned()
    {
        var tree = Tree(
            Selection("b.txt"),
            Entry(StatusEntryKind.Unmerged, "a.txt"),
            Entry(StatusEntryKind.Ordinary, "b.txt"));

        Assert.Null(ConflictedSelection.Find(tree));
    }

    [Fact]
    public void GivenAFolderRow_WhenFinding_ThenNothingIsReturned()
    {
        var tree = Tree(
            new WorkingTreeSelection(WorkingTreeSection.Unstaged, "a.txt", 0, true),
            Entry(StatusEntryKind.Unmerged, "a.txt"));

        Assert.Null(ConflictedSelection.Find(tree));
    }

    [Fact]
    public void GivenTheStagedList_WhenFinding_ThenNothingIsReturned()
    {
        var tree = Tree(
            new WorkingTreeSelection(WorkingTreeSection.Staged, "a.txt", 0),
            Entry(StatusEntryKind.Unmerged, "a.txt"));

        Assert.Null(ConflictedSelection.Find(tree));
    }

    [Fact]
    public void GivenNoWorkingTree_WhenFinding_ThenNothingIsReturned()
    {
        Assert.Null(ConflictedSelection.Find(null));
    }

    private static WorkingTreeSelection Selection(string path) => new(WorkingTreeSection.Unstaged, path, 0);

    private static StatusEntry Entry(StatusEntryKind kind, string path) => new(
        kind,
        path,
        null,
        FileChangeKind.Unmodified,
        FileChangeKind.Modified,
        null,
        ConflictKind.None,
        null);

    private static WorkingTreeState Tree(WorkingTreeSelection selection, params StatusEntry[] entries)
    {
        var head = new HeadState(HeadKind.Branch, "main", null);
        var snapshot = new StatusSnapshot(new StatusBranch(head, null, null, null), entries, []);

        return new WorkingTreeState(snapshot, selection);
    }
}
