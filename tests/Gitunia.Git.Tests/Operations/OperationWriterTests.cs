using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using System.Runtime.Versioning;
using Xunit;

namespace Gitunia.Git.Tests.Operations;

public class OperationWriterTests
{
    private const string ConflictFile = "conflict.txt";
    private const string Resolved = "resolved\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenResolvedMerge_WhenContinuing_ThenMergeCommitIsMadeWithItsPreparedMessage()
    {
        using var repository = TestRepositories.Conflicted();
        var (writer, location) = await CreateAsync(repository);
        Resolve(repository);

        var result = await writer.ContinueAsync(location, RepositoryOperation.Merge, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(repository.Head, result.Head);
        Assert.Equal(2, repository.Git("rev-list", "--parents", "-n", "1", "HEAD").Trim().Split(' ').Length - 1);
        Assert.Equal("Merge other", repository.Git("log", "-1", "--format=%s").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenUnresolvedMerge_WhenContinuing_ThenGitRefusesAndTheMergeStays()
    {
        using var repository = TestRepositories.Conflicted();
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.ContinueAsync(location, RepositoryOperation.Merge, Token));

        Assert.Equal(RepositoryOperation.Merge, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenConflictedMerge_WhenAborting_ThenTheOldTipAndACleanTreeReturn()
    {
        using var repository = TestRepositories.Conflicted();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.AbortAsync(location, RepositoryOperation.Merge, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(before, repository.Head);
        Assert.Equal(string.Empty, repository.Git("status", "--porcelain").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenConflictedMerge_WhenSkipping_ThenItIsRefusedBeforeGitRuns()
    {
        using var repository = TestRepositories.Conflicted();
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => writer.SkipAsync(location, RepositoryOperation.Merge, Token));

        Assert.Equal(RepositoryOperation.Merge, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenResolvedRebase_WhenContinuing_ThenRebaseFinishesOnTheBranch()
    {
        using var repository = TestRepositories.RebaseConflicted();
        var (writer, location) = await CreateAsync(repository);
        Resolve(repository);

        var result = await writer.ContinueAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("other", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("Other change", repository.Git("log", "-1", "--format=%s").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenRebaseWithTwoConflictingCommits_WhenContinuing_ThenItStopsAgainAsConflicted()
    {
        using var repository = TwoStepRebase();
        var (writer, location) = await CreateAsync(repository);
        Resolve(repository);

        var result = await writer.ContinueAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Conflicted, result.Outcome);
        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(location));
        Assert.Contains("UU", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenRebaseWithTwoConflictingCommits_WhenSkipping_ThenTheNextCommitStopsAsConflicted()
    {
        using var repository = TwoStepRebase();
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.SkipAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Conflicted, result.Outcome);
        Assert.Equal("Second", repository.Git("show", "-s", "--format=%s", "REBASE_HEAD").Trim());
    }

    [Fact]
    public async Task GivenConflictedRebase_WhenAborting_ThenTheBranchIsBackWhereItWas()
    {
        using var repository = TestRepositories.RebaseConflicted();
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.AbortAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("other", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("Other change", repository.Git("log", "-1", "--format=%s").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenResolvedCherryPick_WhenContinuing_ThenCommitKeepsTheOriginalMessage()
    {
        using var repository = TestRepositories.CherryPickConflicted();
        var (writer, location) = await CreateAsync(repository);
        Resolve(repository);

        var result = await writer.ContinueAsync(location, RepositoryOperation.CherryPick, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("Other change", repository.Git("log", "-1", "--format=%s").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenConflictedCherryPick_WhenSkipping_ThenNothingIsCommittedAndTheTreeIsClean()
    {
        using var repository = TestRepositories.CherryPickConflicted();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        var result = await writer.SkipAsync(location, RepositoryOperation.CherryPick, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(before, repository.Head);
        Assert.Equal(string.Empty, repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task GivenConflictedCherryPick_WhenAborting_ThenTheOldTipReturns()
    {
        using var repository = TestRepositories.CherryPickConflicted();
        var before = repository.Head;
        var (writer, location) = await CreateAsync(repository);

        await writer.AbortAsync(location, RepositoryOperation.CherryPick, Token);

        Assert.Equal(before, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenResolvedRevert_WhenContinuing_ThenRevertCommitIsMadeWithItsPreparedMessage()
    {
        using var repository = TestRepositories.RevertConflicted();
        var (writer, location) = await CreateAsync(repository);
        Resolve(repository);

        var result = await writer.ContinueAsync(location, RepositoryOperation.Revert, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        var subject = repository.Git("log", "-1", "--format=%s").Trim();
        Assert.StartsWith("Revert \"First change\"", subject, StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenConflictedRevert_WhenSkippingAndAborting_ThenHeadNeverMoves()
    {
        using var skipped = TestRepositories.RevertConflicted();
        var skippedBefore = skipped.Head;
        var (skipWriter, skipLocation) = await CreateAsync(skipped);
        await skipWriter.SkipAsync(skipLocation, RepositoryOperation.Revert, Token);

        using var aborted = TestRepositories.RevertConflicted();
        var abortedBefore = aborted.Head;
        var (abortWriter, abortLocation) = await CreateAsync(aborted);
        await abortWriter.AbortAsync(abortLocation, RepositoryOperation.Revert, Token);

        Assert.Equal(skippedBefore, skipped.Head);
        Assert.Equal(abortedBefore, aborted.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(skipLocation));
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(abortLocation));
    }

    [Theory]
    [InlineData(RepositoryOperation.Merge)]
    [InlineData(RepositoryOperation.Rebase)]
    [InlineData(RepositoryOperation.CherryPick)]
    [InlineData(RepositoryOperation.Revert)]
    [UnsupportedOSPlatform("windows")]
    public async Task GivenAnEditorInTheEnvironment_WhenContinuing_ThenItIsNeverStarted(RepositoryOperation operation)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The recording editor is a shell script");
        using var repository = operation switch
        {
            RepositoryOperation.Merge => TestRepositories.Conflicted(),
            RepositoryOperation.Rebase => TestRepositories.RebaseConflicted(),
            RepositoryOperation.CherryPick => TestRepositories.CherryPickConflicted(),
            _ => TestRepositories.RevertConflicted(),
        };
        var marker = Path.Combine(repository.WorkingTreePath, "..", $"editor-ran-{Guid.NewGuid():N}");
        var script = Path.Combine(repository.WorkingTreePath, "..", $"editor-{Guid.NewGuid():N}.sh");
        await File.WriteAllTextAsync(script, $"#!/bin/sh\ntouch \"{marker}\"\n", Token);
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var configured = new Dictionary<string, string?> { ["VISUAL"] = script, ["EDITOR"] = script };
        var (writer, location) = await CreateAsync(repository, configured);
        Resolve(repository);

        var result = await writer.ContinueAsync(location, operation, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.False(File.Exists(marker));
    }

    private static TestRepository TwoStepRebase()
    {
        var repository = TestRepository.Create()
            .Commit("Initial", new TestFile(ConflictFile, "original\n"))
            .CheckoutNewBranch("other")
            .Commit("First", new TestFile(ConflictFile, "first\n"))
            .Commit("Second", new TestFile(ConflictFile, "second\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile(ConflictFile, "main\n"))
            .Checkout("other");
        return repository.RebaseExpectingConflict("main");
    }

    private static void Resolve(TestRepository repository)
    {
        repository.WriteFile(ConflictFile, Resolved);
        repository.Git("add", ConflictFile);
    }

    private static async Task<Harness> CreateAsync(
        TestRepository repository,
        IReadOnlyDictionary<string, string?>? extraEnvironment = null)
    {
        var runner = repository.CreateIsolatedRunner(extraEnvironment);
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var detector = new RepositoryOperationDetector();
        return new Harness(new OperationWriter(new OperationRunner(runner, detector)), location);
    }

    private sealed record Harness(OperationWriter Writer, RepositoryLocation Location);
}
