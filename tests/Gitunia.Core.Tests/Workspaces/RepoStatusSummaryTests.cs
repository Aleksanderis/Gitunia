using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces;

public class RepoStatusSummaryTests
{
    [Fact]
    public void GivenCleanStatusWithoutUpstream_WhenSummarizing_ThenNotDirtyAndZeroCounts()
    {
        var summary = RepoStatusSummary.From(new StatusSnapshot(BranchFor(null, null), [], []));

        Assert.Equal(new RepoStatusSummary(false, 0, 0), summary);
    }

    [Fact]
    public void GivenEntriesAndDivergence_WhenSummarizing_ThenDirtyWithCounts()
    {
        var entry = new StatusEntry(StatusEntryKind.Untracked, "a.txt", null, FileChangeKind.Unmodified, FileChangeKind.Unmodified, null, ConflictKind.None, null);

        var summary = RepoStatusSummary.From(new StatusSnapshot(BranchFor(2, 5), [entry], []));

        Assert.Equal(new RepoStatusSummary(true, 2, 5), summary);
    }

    private static StatusBranch BranchFor(int? ahead, int? behind) => new(new HeadState(HeadKind.Branch, "main", null), null, ahead, behind);
}
