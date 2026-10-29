using Gitunia.Core.Editing;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class CommitCheckTests
{
    [Fact]
    public void GivenNoStatus_WhenChecking_ThenItIsRefusedAsNotLoaded()
    {
        Assert.Equal(CommitCheck.StatusNotLoaded, CommitCheck.Refusal(Draft("Message"), null, RepositoryOperation.None));
    }

    [Fact]
    public void GivenABlankMessage_WhenChecking_ThenItIsRefused()
    {
        Assert.Equal(CommitCheck.EmptyMessage, CommitCheck.Refusal(Draft(" \n "), Tree(staged: true), RepositoryOperation.None));
    }

    [Fact]
    public void GivenNothingStaged_WhenChecking_ThenItIsRefusedUnlessAmending()
    {
        Assert.Equal(CommitCheck.NothingStaged, CommitCheck.Refusal(Draft("Message"), Tree(staged: false), RepositoryOperation.None));

        var amend = Draft("Message");
        amend.SetAmend(true);
        Assert.Null(CommitCheck.Refusal(amend, Tree(staged: false), RepositoryOperation.None));
    }

    [Theory]
    [InlineData(RepositoryOperation.Merge)]
    [InlineData(RepositoryOperation.CherryPick)]
    [InlineData(RepositoryOperation.Revert)]
    public void GivenAnOperationThatEndsWithACommit_WhenNothingIsStaged_ThenTheCommitIsAllowed(RepositoryOperation operation)
    {
        Assert.Null(CommitCheck.Refusal(Draft("Message"), Tree(staged: false), operation));
    }

    [Fact]
    public void GivenARebase_WhenNothingIsStaged_ThenItIsStillRefused()
    {
        Assert.Equal(CommitCheck.NothingStaged, CommitCheck.Refusal(Draft("Message"), Tree(staged: false), RepositoryOperation.Rebase));
    }

    [Fact]
    public void GivenAnUnbornBranch_WhenAmending_ThenThereIsNothingToAmend()
    {
        var amend = Draft("Message");
        amend.SetAmend(true);

        Assert.Equal(CommitCheck.NothingToAmend, CommitCheck.Refusal(amend, Tree(staged: true, HeadKind.Unborn), RepositoryOperation.None));
        Assert.Equal(CommitCheck.NothingToAmend, CommitCheck.AmendRefusal(Tree(staged: true, HeadKind.Unborn), RepositoryOperation.None));
    }

    [Fact]
    public void GivenAMerge_WhenSwitchingAmendOn_ThenItIsRefused()
    {
        Assert.Equal(CommitCheck.AmendDuringMerge, CommitCheck.AmendRefusal(Tree(staged: true), RepositoryOperation.Merge));
        Assert.Null(CommitCheck.AmendRefusal(Tree(staged: true), RepositoryOperation.None));
    }

    [Theory]
    [InlineData("origin/main", 0, true)]
    [InlineData("origin/main", 2, false)]
    [InlineData(null, null, false)]
    public void GivenAnUpstream_WhenAsking_ThenOnlyAHeadThatIsOnItNeedsAForcePush(string? upstream, int? ahead, bool expected)
    {
        Assert.Equal(expected, CommitCheck.NeedsForcePush(Tree(staged: true, upstream: upstream, ahead: ahead)));
    }

    private static CommitDraft Draft(string message)
    {
        var draft = new CommitDraft();
        draft.Editor.SetText(message);
        return draft;
    }

    private static WorkingTreeState Tree(bool staged, HeadKind head = HeadKind.Branch, string? upstream = null, int? ahead = null)
    {
        StatusEntry[] entries = [];

        if (staged)
        {
            entries =
            [
                new(
                    StatusEntryKind.Ordinary,
                    "a.txt",
                    null,
                    FileChangeKind.Modified,
                    FileChangeKind.Unmodified,
                    null,
                    ConflictKind.None,
                    null),
            ];
        }

        var branch = new StatusBranch(new HeadState(head, "main", null), upstream, ahead, null);
        return WorkingTreeState.Reconcile(null, new StatusSnapshot(branch, entries, []));
    }
}
