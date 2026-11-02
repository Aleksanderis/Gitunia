using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Core.Conflicts;
using Xunit;

namespace Gitunia.Core.Tests.Conflicts;

[Trait("Os", "Independent")]
public class ConflictedSelectionTests
{
    public static TheoryData<string, WorkingTreeState?> NoConflictedSelection() => new()
    {
        { "a plain modified file", Tree(Selection("a.txt"), Entry(StatusEntryKind.Ordinary, "a.txt")) },
        {
            "another file than the conflicted one",
            Tree(
                Selection("b.txt"),
                Entry(StatusEntryKind.Unmerged, "a.txt"),
                Entry(StatusEntryKind.Ordinary, "b.txt"))
        },
        {
            "a folder row",
            Tree(
                new WorkingTreeSelection(WorkingTreeSection.Unstaged, "a.txt", 0, true),
                Entry(StatusEntryKind.Unmerged, "a.txt"))
        },
        {
            "the staged list",
            Tree(
                new WorkingTreeSelection(WorkingTreeSection.Staged, "a.txt", 0),
                Entry(StatusEntryKind.Unmerged, "a.txt"))
        },
        { "no working tree", null },
    };


    [Fact]
    public void GivenTheCursorOnAConflictedFile_WhenFinding_ThenTheEntryIsReturned()
    {
        var tree = Tree(Selection("a.txt"), Entry(StatusEntryKind.Unmerged, "a.txt"));

        Assert.Equal("a.txt", ConflictedSelection.Find(tree)?.Path);
    }

    [Theory]
    [MemberData(nameof(NoConflictedSelection))]
    public void GivenNoConflictedFileUnderTheCursor_WhenFinding_ThenNothingIsReturned(string reason, WorkingTreeState? tree)
    {
        Assert.NotEmpty(reason);
        Assert.Null(ConflictedSelection.Find(tree));
    }

    [Fact]
    public void GivenDeleteModifyFirstAndAddAddSecond_WhenFindingTheFirst_ThenTheAddAddIsPreferred()
    {
        var tree = Tree(
            Selection("z.txt"),
            Entry(StatusEntryKind.Unmerged, "a.txt", ConflictKind.DeletedByThem),
            Entry(StatusEntryKind.Unmerged, "b.txt", ConflictKind.BothAdded));

        Assert.Equal("b.txt", ConflictedSelection.FindFirst(tree)?.Path);
    }

    [Fact]
    public void GivenOnlyDeleteModify_WhenFindingTheFirst_ThenItIsStillReturnedSoTheRefusalCanSayWhy()
    {
        var tree = Tree(Selection("z.txt"), Entry(StatusEntryKind.Unmerged, "a.txt", ConflictKind.DeletedByUs));

        Assert.Equal("a.txt", ConflictedSelection.FindFirst(tree)?.Path);
    }

    private static WorkingTreeSelection Selection(string path) => new(WorkingTreeSection.Unstaged, path, 0);

    private static StatusEntry Entry(StatusEntryKind kind, string path, ConflictKind conflict = ConflictKind.None) =>
        new(kind, path, null, FileChangeKind.Unmodified, FileChangeKind.Modified, null, conflict, null);

    private static WorkingTreeState Tree(WorkingTreeSelection selection, params StatusEntry[] entries)
    {
        var head = new HeadState(HeadKind.Branch, "main", null);
        var snapshot = new StatusSnapshot(new StatusBranch(head, null, null, null), entries, []);

        return new WorkingTreeState(snapshot, selection);
    }
}
