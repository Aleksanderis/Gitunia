using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Repositories;

[Trait("Os", "Independent")]
public class RepositoryStatusSummaryTests
{
    [Fact]
    public void GivenCleanStatusWithoutUpstream_WhenSummarizing_ThenNotDirtyAndZeroCounts()
    {
        var summary = RepositoryStatusSummary.From(new StatusSnapshot(BranchFor(null, null), [], []));

        Assert.Equal(new RepositoryStatusSummary(false, 0, 0), summary);
    }

    [Fact]
    public void GivenEntriesAndDivergence_WhenSummarizing_ThenDirtyWithCounts()
    {
        var entry = new StatusEntry(StatusEntryKind.Untracked, "a.txt", null, FileChangeKind.Unmodified, FileChangeKind.Unmodified, null, ConflictKind.None, null);

        var summary = RepositoryStatusSummary.From(new StatusSnapshot(BranchFor(2, 5), [entry], []));

        Assert.Equal(new RepositoryStatusSummary(true, 2, 5), summary);
    }

    private static StatusBranch BranchFor(int? ahead, int? behind) => new(new HeadState(HeadKind.Branch, "main", null), null, ahead, behind);
}
