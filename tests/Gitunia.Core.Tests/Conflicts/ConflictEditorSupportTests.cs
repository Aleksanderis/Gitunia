using Gitunia.Core.Conflicts;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Conflicts;

[Trait("Os", "Independent")]
public class ConflictEditorSupportTests
{
    [Theory]
    [InlineData(ConflictKind.BothModified, true)]
    [InlineData(ConflictKind.BothAdded, true)]
    [InlineData(ConflictKind.DeletedByUs, false)]
    [InlineData(ConflictKind.DeletedByThem, false)]
    [InlineData(ConflictKind.BothDeleted, false)]
    [InlineData(ConflictKind.AddedByUs, false)]
    [InlineData(ConflictKind.AddedByThem, false)]
    public void GivenAConflictKind_WhenAsking_ThenOnlyBothModifiedAndBothAddedOpen(ConflictKind kind, bool opens)
    {
        Assert.Equal(opens, ConflictEditorSupport.CanOpen(kind));
    }

    [Fact]
    public void GivenOnlyDeleteModifyConflicts_WhenAskingIfAnyOpens_ThenNo()
    {
        var tree = Tree(Entry("a.txt", ConflictKind.DeletedByThem), Entry("b.txt", ConflictKind.DeletedByUs));

        Assert.False(ConflictEditorSupport.AnyCanOpen(tree));
    }

    [Fact]
    public void GivenAnAddAddAmongDeleteModify_WhenAskingIfAnyOpens_ThenYes()
    {
        var tree = Tree(Entry("a.txt", ConflictKind.DeletedByThem), Entry("b.txt", ConflictKind.BothAdded));

        Assert.True(ConflictEditorSupport.AnyCanOpen(tree));
    }

    [Fact]
    public void GivenNoWorkingTree_WhenAskingIfAnyOpens_ThenNo()
    {
        Assert.False(ConflictEditorSupport.AnyCanOpen(null));
    }

    [Theory]
    [InlineData(ConflictKind.DeletedByUs, "delete/modify")]
    [InlineData(ConflictKind.DeletedByThem, "delete/modify")]
    public void GivenARefusedKind_WhenWordingTheRefusal_ThenItNamesTheKind(ConflictKind kind, string expected)
    {
        Assert.Contains(expected, ConflictEditorSupport.Refusal(kind), StringComparison.Ordinal);
    }

    private static StatusEntry Entry(string path, ConflictKind conflict) => new(
        StatusEntryKind.Unmerged,
        path,
        null,
        FileChangeKind.Unmodified,
        FileChangeKind.Modified,
        null,
        conflict,
        null);

    private static WorkingTreeState Tree(params StatusEntry[] entries)
    {
        var head = new HeadState(HeadKind.Branch, "main", null);
        var snapshot = new StatusSnapshot(new StatusBranch(head, null, null, null), entries, []);

        return new WorkingTreeState(snapshot, null);
    }
}
