using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Repositories;

public class WorktreeStatusTests
{
    [Fact]
    public void GivenMixedEntries_WhenCounted_ThenEachPathIsInTheGroupStatusListsItUnder()
    {
        var staged = Entry(StatusEntryKind.Ordinary, FileChangeKind.Added, FileChangeKind.Unmodified);
        var both = Entry(StatusEntryKind.Ordinary, FileChangeKind.Modified, FileChangeKind.Modified);
        var unstaged = Entry(StatusEntryKind.Ordinary, FileChangeKind.Unmodified, FileChangeKind.Deleted);
        var untracked = Entry(StatusEntryKind.Untracked, FileChangeKind.Unmodified, FileChangeKind.Unmodified);
        var ignored = Entry(StatusEntryKind.Ignored, FileChangeKind.Unmodified, FileChangeKind.Unmodified);
        var conflict = Entry(StatusEntryKind.Unmerged, FileChangeKind.Unmodified, FileChangeKind.Unmodified);
        var head = new StatusBranch(new HeadState(HeadKind.Branch, "main", null), "origin/main", 2, 3);

        var entries = new[] { staged, both, unstaged, untracked, ignored, conflict };

        var status = WorktreeStatus.From(new StatusSnapshot(head, entries, []));

        Assert.Equal(new WorktreeStatus(2, 3, 1, 2, 3), status);
        Assert.True(status.IsDirty);
    }

    [Fact]
    public void GivenOnlyIgnoredEntriesAndNoUpstream_WhenCounted_ThenCleanWithZeroTracking()
    {
        var head = new StatusBranch(new HeadState(HeadKind.Branch, "main", null), null, null, null);
        var ignored = Entry(StatusEntryKind.Ignored, FileChangeKind.Unmodified, FileChangeKind.Unmodified);

        var status = WorktreeStatus.From(new StatusSnapshot(head, [ignored], []));

        Assert.Equal(new WorktreeStatus(0, 0, 0, 0, 0), status);
        Assert.False(status.IsDirty);
    }

    private static StatusEntry Entry(StatusEntryKind kind, FileChangeKind index, FileChangeKind worktree) =>
        new(kind, "path.txt", null, index, worktree, null, ConflictKind.None, null);
}
