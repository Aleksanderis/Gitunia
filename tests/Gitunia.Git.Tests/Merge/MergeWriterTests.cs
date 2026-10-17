using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Merge;
using Gitunia.Git.Operations;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Merge;

public class MergeWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenABranchAheadOfHead_WhenMerging_ThenItFastForwardsWithoutACommit()
    {
        using var repository = AheadBranch();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "topic", MergeMode.Default, Token);

        Assert.Equal(MergeKind.FastForward, result.Kind);
        Assert.Equal(before, result.OldHead);
        Assert.Equal(repository.Head, result.Head);
        Assert.Equal(repository.Git("rev-parse", "topic").Trim(), repository.Head);
    }

    [Fact]
    public async Task GivenABranchAheadOfHead_WhenMergingWithNoFastForward_ThenAMergeCommitIsMadeWithGitsMessage()
    {
        using var repository = AheadBranch();
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "topic", MergeMode.NoFastForward, Token);

        Assert.Equal(MergeKind.MergeCommit, result.Kind);
        Assert.Equal(2, ParentCount(repository));
        Assert.Equal("Merge branch 'topic'", repository.Git("log", "-1", "--format=%s").Trim());
    }

    [Fact]
    public async Task GivenATagWithTheNameOfTheBranch_WhenMergingTheFullRef_ThenTheBranchIsMergedWithTheShortNameMessage()
    {
        using var repository = AheadBranch();
        var topic = repository.Git("rev-parse", "refs/heads/topic").Trim();
        repository.Git("tag", "topic", repository.Head);
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "refs/heads/topic", MergeMode.NoFastForward, Token);

        Assert.Equal(MergeKind.MergeCommit, result.Kind);
        Assert.Equal(topic, repository.Git("rev-parse", "HEAD^2").Trim());
        Assert.Equal("Merge branch 'topic'", repository.Git("log", "-1", "--format=%s").Trim());
    }

    [Fact]
    public async Task GivenARemoteTrackingRef_WhenMergingTheFullRef_ThenTheMessageNamesTheRemoteBranch()
    {
        using var repository = AheadBranch();
        repository.Git("update-ref", "refs/remotes/origin/topic", "refs/heads/topic");
        var (writer, location) = await CreateAsync(repository);

        await writer.MergeAsync(location, "refs/remotes/origin/topic", MergeMode.NoFastForward, Token);

        Assert.Equal("Merge remote-tracking branch 'origin/topic'", repository.Git("log", "-1", "--format=%s").Trim());
    }

    [Fact]
    public async Task GivenDivergedBranches_WhenMergingByHash_ThenAMergeCommitIsMade()
    {
        using var repository = AheadBranch().Commit("three", new TestFile("c.txt", "3"));
        var (writer, location) = await CreateAsync(repository);
        var topic = repository.Git("rev-parse", "topic").Trim();

        var result = await writer.MergeAsync(location, topic, MergeMode.Default, Token);

        Assert.Equal(MergeKind.MergeCommit, result.Kind);
        Assert.Equal(2, ParentCount(repository));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
    }

    [Fact]
    public async Task GivenABranchAlreadyContained_WhenMerging_ThenItIsUpToDateAndNothingMoves()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Branch("topic")
            .Commit("two", new TestFile("a.txt", "2"));
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "topic", MergeMode.Default, Token);

        Assert.Equal(MergeKind.UpToDate, result.Kind);
        Assert.Equal(before, repository.Head);
    }

    [Fact]
    public async Task GivenABranchAheadOfHead_WhenSquashing_ThenTheChangesAreStagedAndNothingIsCommittedOrInProgress()
    {
        using var repository = AheadBranch();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "topic", MergeMode.Squash, Token);

        Assert.Equal(MergeKind.Squashed, result.Kind);
        Assert.Equal(before, repository.Head);
        Assert.Equal(before, result.Head);
        Assert.Contains("A  b.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenABranchAlreadyContained_WhenSquashing_ThenItIsUpToDate()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Branch("topic")
            .Commit("two", new TestFile("a.txt", "2"));
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "topic", MergeMode.Squash, Token);

        Assert.Equal(MergeKind.UpToDate, result.Kind);
    }

    [Fact]
    public async Task GivenConflictingBranches_WhenMerging_ThenTheMergeIsInProgressAsAResultNotAFailure()
    {
        using var repository = TestRepositories.ConflictingBranches();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "other", MergeMode.Default, Token);

        Assert.Equal(MergeKind.Conflicted, result.Kind);
        Assert.Equal(before, result.OldHead);
        Assert.Equal(RepositoryOperation.Merge, new RepositoryOperationDetector().Detect(location));
        Assert.Contains("UU conflict.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenConflictingBranches_WhenSquashing_ThenFilesAreUnmergedButNoOperationIsInProgress()
    {
        using var repository = TestRepositories.ConflictingBranches();
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "other", MergeMode.Squash, Token);

        Assert.Equal(MergeKind.Conflicted, result.Kind);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
        Assert.Contains("UU conflict.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MergeMode.Default)]
    [InlineData(MergeMode.Squash)]
    public async Task GivenAnUnknownTarget_WhenMerging_ThenGitsErrorIsThrown(MergeMode mode)
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.MergeAsync(location, "missing", mode, Token));
    }

    [Fact]
    public async Task GivenALocalChangeGitCannotMergeOver_WhenMerging_ThenItThrowsAndNothingChanges()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("two", new TestFile("a.txt", "topic"))
            .Checkout("main")
            .WriteFile("a.txt", "dirty");
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.MergeAsync(location, "topic", MergeMode.Default, Token));

        Assert.Equal("dirty", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenMerging_ThenNothingRuns()
    {
        using var repository = AheadBranch();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.MergeAsync(location, "topic", MergeMode.Default, cancelled.Token));

        Assert.Equal(before, repository.Head);
    }

    [Fact]
    public async Task GivenATargetThatIsAMergeCommitAheadOfHead_WhenMerging_ThenItIsAFastForward()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("three", new TestFile("c.txt", "3"))
            .Checkout("topic")
            .Merge("main")
            .Checkout("main");
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "topic", MergeMode.Default, Token);

        Assert.Equal(MergeKind.FastForward, result.Kind);
        Assert.Equal(repository.Git("rev-parse", "topic").Trim(), repository.Head);
    }

    [Fact]
    public async Task GivenSomethingStagedAndAContainedTarget_WhenSquashing_ThenItIsUpToDateNotSquashed()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Branch("topic")
            .Commit("two", new TestFile("a.txt", "2"))
            .WriteFile("staged.txt", "mine");
        repository.Git("add", "staged.txt");
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.MergeAsync(location, "topic", MergeMode.Squash, Token);

        Assert.Equal(MergeKind.UpToDate, result.Kind);
    }

    [Fact]
    public async Task GivenAConflictedMerge_WhenResolvedAndContinued_ThenAMergeCommitWithTwoParentsIsMade()
    {
        using var repository = TestRepositories.ConflictingBranches();
        var (writer, location) = await CreateAsync(repository);
        await writer.MergeAsync(location, "other", MergeMode.Default, Token);
        repository.WriteFile("conflict.txt", "resolved\n");
        repository.Git("add", "conflict.txt");
        var operationWriter = new OperationWriter(new OperationRunner(
            repository.CreateIsolatedRunner(),
            new RepositoryOperationDetector()));

        await operationWriter.ContinueAsync(location, RepositoryOperation.Merge, Token);

        Assert.Equal(2, ParentCount(repository));
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAConflictedMerge_WhenAborted_ThenTheOldTipReturns()
    {
        using var repository = TestRepositories.ConflictingBranches();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);
        await writer.MergeAsync(location, "other", MergeMode.Default, Token);
        var operationWriter = new OperationWriter(new OperationRunner(
            repository.CreateIsolatedRunner(),
            new RepositoryOperationDetector()));

        await operationWriter.AbortAsync(location, RepositoryOperation.Merge, Token);

        Assert.Equal(before, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    private static TestRepository AheadBranch() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("two", new TestFile("b.txt", "2"))
            .Checkout("main");

    private static int ParentCount(TestRepository repository) =>
        repository.Git("rev-list", "--parents", "-n", "1", "HEAD").Trim().Split(' ').Length - 1;

    private static async Task<(MergeWriter Writer, RepositoryLocation Location)> CreateAsync(TestRepository repository)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var operations = new OperationRunner(runner, new RepositoryOperationDetector());
        return (new MergeWriter(runner, operations), location);
    }
}
