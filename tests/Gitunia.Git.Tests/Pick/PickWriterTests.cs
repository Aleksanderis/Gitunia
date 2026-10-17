using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Pick;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Pick;

public class PickWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenACommitOfAnotherBranch_WhenCherryPicking_ThenItIsAppliedWithTheOriginalMessage()
    {
        using var repository = Diverged();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.PickAsync(location, PickKind.CherryPick, [Hash(repository, "topic")], null, Token);

        Assert.Equal(PickOutcome.Completed, result.Outcome);
        Assert.Equal(before, result.OldHead);
        Assert.Equal(repository.Head, result.Head);
        Assert.Equal("topic two", repository.Git("log", "-1", "--format=%s").Trim());
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenTwoCommits_WhenCherryPickingInTheGivenOrder_ThenTheyLandOldestFirst()
    {
        using var repository = Diverged().Checkout("topic")
            .Commit("topic three", new TestFile("c.txt", "3"))
            .Checkout("main");
        var (writer, location) = await CreateAsync(repository);
        var hashes = new[] { Hash(repository, "topic~1"), Hash(repository, "topic") };

        var result = await writer.PickAsync(location, PickKind.CherryPick, hashes, null, Token);

        Assert.Equal(PickOutcome.Completed, result.Outcome);
        Assert.Equal("topic three\ntopic two", repository.Git("log", "-2", "--format=%s").Trim().Replace("\r", ""));
    }

    [Fact]
    public async Task GivenACommitHeadAlreadyHas_WhenCherryPicking_ThenNothingIsAppliedAndNoOperationRemains()
    {
        using var repository = Diverged();
        var (writer, location) = await CreateAsync(repository);
        await writer.PickAsync(location, PickKind.CherryPick, [Hash(repository, "topic")], null, Token);
        var after = repository.Head;

        var result = await writer.PickAsync(location, PickKind.CherryPick, [Hash(repository, "topic")], null, Token);

        Assert.Equal(PickOutcome.NothingToApply, result.Outcome);
        Assert.Equal(after, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAConflictingCommit_WhenCherryPicking_ThenThePickIsInProgressAsAResultNotAFailure()
    {
        using var repository = TestRepositories.ConflictingBranches();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.PickAsync(location, PickKind.CherryPick, [Hash(repository, "other")], null, Token);

        Assert.Equal(PickOutcome.Conflicted, result.Outcome);
        Assert.Equal(before, result.OldHead);
        Assert.Equal(RepositoryOperation.CherryPick, new RepositoryOperationDetector().Detect(location));
        Assert.Contains("UU conflict.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAConflictOnTheSecondOfTwoCommits_WhenCherryPicking_ThenTheFirstIsKeptAndTheRestWaits()
    {
        using var repository = TestRepositories.ConflictingBranches().Checkout("other")
            .Commit("other three", new TestFile("c.txt", "3"))
            .Checkout("main");
        var (writer, location) = await CreateAsync(repository);
        var hashes = new[] { Hash(repository, "other~1"), Hash(repository, "other") };

        var result = await writer.PickAsync(location, PickKind.CherryPick, hashes, null, Token);

        Assert.Equal(PickOutcome.Conflicted, result.Outcome);
        Assert.Equal(RepositoryOperation.CherryPick, new RepositoryOperationDetector().Detect(location));
        Assert.True(Directory.Exists(Path.Combine(repository.WorkingTreePath, ".git", "sequencer")));
    }

    [Fact]
    public async Task GivenAConflictedCherryPick_WhenAborted_ThenTheOldTipReturns()
    {
        using var repository = TestRepositories.ConflictingBranches();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);
        await writer.PickAsync(location, PickKind.CherryPick, [Hash(repository, "other")], null, Token);
        var operationWriter = new OperationWriter(new OperationRunner(
            repository.CreateIsolatedRunner(),
            new RepositoryOperationDetector()));

        await operationWriter.AbortAsync(location, RepositoryOperation.CherryPick, Token);

        Assert.Equal(before, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenACommit_WhenReverting_ThenANewCommitUndoesItWithGitsMessage()
    {
        using var repository = Diverged();
        var tip = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.PickAsync(location, PickKind.Revert, [tip], null, Token);

        Assert.Equal(PickOutcome.Completed, result.Outcome);
        Assert.Equal(tip, result.OldHead);
        Assert.NotEqual(tip, repository.Head);
        var subject = repository.Git("log", "-1", "--format=%s").Trim();
        Assert.StartsWith("Revert \"main two\"", subject, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "m.txt")));
    }

    [Fact]
    public async Task GivenARevertThatConflicts_WhenReverting_ThenTheRevertIsInProgressAsAResult()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .Commit("First change", new TestFile("conflict.txt", "first\n"))
            .Commit("Second change", new TestFile("conflict.txt", "second\n"));
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.PickAsync(location, PickKind.Revert, [Hash(repository, "HEAD~1")], null, Token);

        Assert.Equal(PickOutcome.Conflicted, result.Outcome);
        Assert.Equal(RepositoryOperation.Revert, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenTwoCommits_WhenRevertingNewestFirst_ThenBothAreUndone()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("b.txt", "2"))
            .Commit("three", new TestFile("c.txt", "3"));
        var (writer, location) = await CreateAsync(repository);
        var hashes = new[] { Hash(repository, "HEAD"), Hash(repository, "HEAD~1") };

        var result = await writer.PickAsync(location, PickKind.Revert, hashes, null, Token);

        Assert.Equal(PickOutcome.Completed, result.Outcome);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "c.txt")));
    }

    [Theory]
    [InlineData(PickKind.CherryPick)]
    [InlineData(PickKind.Revert)]
    public async Task GivenAMergeCommitWithoutAMainline_WhenPicking_ThenGitsErrorIsThrown(PickKind kind)
    {
        using var repository = MergedTopic();
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.PickAsync(location, kind, [repository.Head], null, Token));

        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAMergeCommitAndAMainline_WhenReverting_ThenTheChangesOfTheOtherParentAreUndone()
    {
        using var repository = MergedTopic();
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.PickAsync(location, PickKind.Revert, [repository.Head], 1, Token);

        Assert.Equal(PickOutcome.Completed, result.Outcome);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "m.txt")));
    }

    [Fact]
    public async Task GivenAMergeCommitAndAMainline_WhenCherryPicking_ThenItsChangesAreCarriedOver()
    {
        using var repository = MergedTopic();
        var merge = repository.Head;
        repository.Checkout("base");
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.PickAsync(location, PickKind.CherryPick, [merge], 1, Token);

        Assert.Equal(PickOutcome.Completed, result.Outcome);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
    }

    [Fact]
    public async Task GivenAMergeAndAPlainCommit_WhenPickingWithParentOne_ThenBothAreApplied()
    {
        using var repository = MergedTopic().Commit("after", new TestFile("d.txt", "4"));
        var (writer, location) = await CreateAsync(repository);
        var hashes = new[] { Hash(repository, "HEAD"), Hash(repository, "HEAD~1") };

        var result = await writer.PickAsync(location, PickKind.Revert, hashes, 1, Token);

        Assert.Equal(PickOutcome.Completed, result.Outcome);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "d.txt")));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
    }

    [Fact]
    public async Task GivenAMergeAndAPlainCommit_WhenPickingWithParentTwo_ThenTheRevertIsLeftInProgress()
    {
        using var repository = MergedTopic().Commit("after", new TestFile("d.txt", "4"));
        var (writer, location) = await CreateAsync(repository);
        var hashes = new[] { Hash(repository, "HEAD"), Hash(repository, "HEAD~1") };

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.PickAsync(location, PickKind.Revert, hashes, 2, Token));

        // Why the UI accepts only parent 1 for such a range: the call fails after the first commit was reverted.
        Assert.Equal(RepositoryOperation.Revert, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenACommitAlreadyReverted_WhenRevertingAgain_ThenGitsErrorIsThrownAndNoOperationRemains()
    {
        using var repository = Diverged();
        var tip = repository.Head;
        var (writer, location) = await CreateAsync(repository);
        await writer.PickAsync(location, PickKind.Revert, [tip], null, Token);
        var after = repository.Head;

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.PickAsync(location, PickKind.Revert, [tip], null, Token));

        Assert.Equal(after, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenADirtyTreeGitCannotPickOver_WhenCherryPicking_ThenGitsErrorIsThrown()
    {
        using var repository = Diverged().WriteFile("b.txt", "dirty");
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.PickAsync(location, PickKind.CherryPick, [Hash(repository, "topic")], null, Token));
    }

    [Fact]
    public async Task GivenAnUnknownCommit_WhenPicking_ThenGitsErrorIsThrown()
    {
        using var repository = Diverged();
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.PickAsync(location, PickKind.CherryPick, [new string('a', 40)], null, Token));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenPicking_ThenNothingRuns()
    {
        using var repository = Diverged();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.PickAsync(location, PickKind.CherryPick, [Hash(repository, "topic")], null, source.Token));

        Assert.Equal(before, repository.Head);
    }

    // Main has "main two" (m.txt), topic has "topic two" (b.txt), both forked from "one".
    private static TestRepository Diverged() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"));

    // Branch base at "one", then main gets m.txt and merges topic (b.txt): the merge commit is HEAD.
    private static TestRepository MergedTopic() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Branch("base")
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"))
            .Merge("topic");

    private static string Hash(TestRepository repository, string revision) =>
        repository.Git("rev-parse", revision).Trim();

    private static async Task<(PickWriter Writer, RepositoryLocation Location)> CreateAsync(TestRepository repository)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var operations = new OperationRunner(runner, new RepositoryOperationDetector());
        return (new PickWriter(runner, operations), location);
    }
}
