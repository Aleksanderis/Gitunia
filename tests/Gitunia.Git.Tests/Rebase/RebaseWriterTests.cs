using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Rebase;

public class RebaseWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenDivergedBranch_WhenRebasing_ThenCommitsAreReplayedOnTheTargetAndTheOldTipStaysInOrigHead()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        var mainTip = repository.Git("rev-parse", "main").Trim();
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.RebaseAsync(location, mainTip, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(repository.Head, result.Head);
        Assert.NotEqual(oldTip, repository.Head);
        Assert.Equal(mainTip, repository.Git("rev-parse", "HEAD~1").Trim());
        Assert.Equal("topic", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(oldTip, repository.Git("rev-parse", "ORIG_HEAD").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAnAnnotatedTag_WhenRebasingOntoIt_ThenTheTaggedCommitIsTheBase()
    {
        using var repository = Diverged();
        var mainTip = repository.Git("rev-parse", "main").Trim();
        repository.Git("tag", "-a", "-m", "release", "v1", mainTip);
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.RebaseAsync(location, "v1", Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(mainTip, repository.Git("rev-parse", "HEAD~1").Trim());
    }

    [Fact]
    public async Task GivenConflictingBranch_WhenRebasing_ThenItStopsAsAResultAndAbortRestoresTheOldTip()
    {
        using var repository = TestRepositories.ConflictingBranches().Checkout("other");
        var oldTip = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.RebaseAsync(location, "main", Token);

        Assert.Equal(OperationOutcome.Conflicted, result.Outcome);
        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(location));
        var unmerged = repository.Git("diff", "--name-only", "--diff-filter=U");
        Assert.Contains("conflict.txt", unmerged, StringComparison.Ordinal);

        var aborted = await new OperationWriter(CreateOperationRunner(repository))
            .AbortAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, aborted.Outcome);
        Assert.Equal(oldTip, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
        Assert.Equal(string.Empty, repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task GivenConflictedRebase_WhenResolvedAndContinued_ThenTheRebaseCompletes()
    {
        using var repository = TestRepositories.ConflictingBranches().Checkout("other");
        var (writer, location) = await CreateAsync(repository);
        await writer.RebaseAsync(location, "main", Token);
        repository.WriteFile("conflict.txt", "resolved\n");
        repository.Git("add", "conflict.txt");

        var result = await new OperationWriter(CreateOperationRunner(repository))
            .ContinueAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("Other change", repository.Git("log", "-1", "--format=%s").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenTrackedChanges_WhenRebasing_ThenGitRefusesWithItsMessageAndNoRebaseStarts()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        repository.WriteFile("topic.txt", "edited");
        var (writer, location) = await CreateAsync(repository);

        var failure = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.RebaseAsync(location, "main", Token));

        Assert.Contains("unstaged changes", failure.StandardError, StringComparison.Ordinal);
        Assert.Equal(oldTip, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAnOptionAsTarget_WhenRebasing_ThenNoGitRuns()
    {
        using var repository = Diverged();
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => writer.RebaseAsync(location, "--abort", Token));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenRebasing_ThenNothingIsRebased()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        var (writer, location) = await CreateAsync(repository);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.RebaseAsync(location, "main", cancelled.Token));

        Assert.Equal(oldTip, repository.Head);
    }

    [Fact]
    public async Task GivenAMergeAmongTheReplayedCommits_WhenCountingMerges_ThenItIsCounted()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .CheckoutNewBranch("side")
            .Commit("Side change", new TestFile("side.txt", "side\n"))
            .Checkout("topic")
            .Commit("Topic change", new TestFile("topic.txt", "topic\n"))
            .Merge("side")
            .Checkout("main")
            .Commit("Main change", new TestFile("main.txt", "main\n"))
            .Checkout("topic");
        var (writer, location) = await CreateAsync(repository);

        var merges = await writer.CountMergesAsync(location, "main", Token);

        Assert.Equal(1, merges);
    }

    [Fact]
    public async Task GivenALinearRange_WhenCountingMerges_ThenItIsZero()
    {
        using var repository = Diverged();
        var (writer, location) = await CreateAsync(repository);

        Assert.Equal(0, await writer.CountMergesAsync(location, "main", Token));
    }

    // Main and topic both moved past the first commit, in different files, so the rebase is clean.
    private static TestRepository Diverged() =>
        TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("Topic change", new TestFile("topic.txt", "topic\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("main.txt", "main\n"))
            .Checkout("topic");

    private static OperationRunner CreateOperationRunner(TestRepository repository) =>
        new(repository.CreateIsolatedRunner(), new RepositoryOperationDetector());

    private static async Task<Harness> CreateAsync(TestRepository repository)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var operations = new OperationRunner(runner, new RepositoryOperationDetector());
        return new Harness(new RebaseWriter(operations, runner), location);
    }

    private sealed record Harness(RebaseWriter Writer, RepositoryLocation Location);
}
