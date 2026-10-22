using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Operations;

public class OperationInfoReaderTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly OperationInfoReader Reader = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenConflictedMerge_WhenReading_ThenTargetIsTheMergedBranch()
    {
        using var repository = TestRepositories.Conflicted();
        repository.Git(["merge", "--abort"], null);
        repository.Git(["merge", "other"], [1]);

        var info = await ReadAsync(repository, RepositoryOperation.Merge);

        Assert.Equal(RepositoryOperation.Merge, info.Kind);
        Assert.Equal("other", info.Target);
    }

    [Fact]
    public async Task GivenMergeWithACustomMessage_WhenReading_ThenTargetIsUnknownNotGuessed()
    {
        using var repository = TestRepositories.Conflicted();

        var info = await ReadAsync(repository, RepositoryOperation.Merge);

        Assert.Null(info.Target);
    }

    [Fact]
    public async Task GivenStoppedRebase_WhenReading_ThenBranchStepCommitAndSubjectAreRead()
    {
        using var repository = TestRepositories.RebaseConflicted();

        var info = await ReadAsync(repository, RepositoryOperation.Rebase);

        Assert.Equal("other", info.Target);
        Assert.Equal(1, info.Step);
        Assert.Equal(1, info.Total);
        Assert.Equal("Other change", info.Subject);
        Assert.Equal(repository.Git("rev-parse", "REBASE_HEAD").Trim(), info.Commit);
        Assert.Equal(repository.Git("rev-parse", "other").Trim(), info.OriginalHead);
    }

    [Fact]
    public async Task GivenStoppedCherryPick_WhenReading_ThenCommitAndSubjectAreRead()
    {
        using var repository = TestRepositories.CherryPickConflicted();

        var info = await ReadAsync(repository, RepositoryOperation.CherryPick);

        Assert.Equal(repository.Git("rev-parse", "other").Trim(), info.Commit);
        Assert.Equal("Other change", info.Subject);
        Assert.Null(info.Remaining);
    }

    [Fact]
    public async Task GivenCherryPickRangeStoppedOnTheFirstCommit_WhenReading_ThenRemainingCountsTheRange()
    {
        using var repository = TestRepositories.Conflicted();
        repository.Git(["merge", "--abort"], null);
        repository.CheckoutNewBranch("picks")
            .Commit("Pick one", new TestFile("conflict.txt", "one\n"))
            .Commit("Pick two", new TestFile("conflict.txt", "two\n"))
            .Checkout("main")
            .Commit("Main again", new TestFile("conflict.txt", "again\n"));
        repository.Git(["cherry-pick", "picks~1", "picks"], [1]);

        var info = await ReadAsync(repository, RepositoryOperation.CherryPick);

        Assert.Equal(2, info.Remaining);
        Assert.Equal("Pick one", info.Subject);
    }

    [Fact]
    public async Task GivenStoppedRevert_WhenReading_ThenSubjectIsTheRevertMessage()
    {
        using var repository = TestRepositories.RevertConflicted();

        var info = await ReadAsync(repository, RepositoryOperation.Revert);

        Assert.StartsWith("Revert \"First change\"", info.Subject, StringComparison.Ordinal);
        Assert.Equal(repository.Git("rev-parse", "HEAD~1").Trim(), info.Commit);
    }

    [Fact]
    public async Task GivenNoOperation_WhenReading_ThenNull()
    {
        using var repository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        Assert.Null(await Reader.ReadAsync(location, RepositoryOperation.None, Token));
    }

    private static async Task<OperationInfo> ReadAsync(TestRepository repository, RepositoryOperation operation)
    {
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var info = await Reader.ReadAsync(location, operation, Token);
        return Assert.IsType<OperationInfo>(info);
    }
}
