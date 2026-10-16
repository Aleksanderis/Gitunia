using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await new RepositoryDiscovery(repository.CreateIsolatedRunner()).DiscoverAsync(repository.WorkingTreePath, Token);

    [Fact]
    public async Task GivenStagedChanges_WhenCommitting_ThenHeadHasTheMessageExactlyAsTyped()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        var result = await writer.CommitAsync(location, new CommitRequest("Subject line\n\n# not a comment\nBody ünï\n", false, false, false), Token);

        Assert.Equal(repository.Head, result.Hash);
        Assert.Null(result.PreviousHash);
        Assert.Equal("Subject line\n\n# not a comment\nBody ünï", repository.Git("log", "-1", "--format=%B").TrimEnd());
    }

    [Fact]
    public async Task GivenSignOff_WhenCommitting_ThenTheTrailerIsAdded()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        await writer.CommitAsync(location, new CommitRequest("Signed", true, false, false), Token);

        Assert.Contains("Signed-off-by: Test <test@example.com>", repository.Git("log", "-1", "--format=%B"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAmend_WhenCommitting_ThenHeadIsReplacedAndThePreviousHashIsReturned()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        var before = repository.Head;
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        var result = await writer.CommitAsync(location, new CommitRequest("Reworded", false, false, true), Token);

        Assert.Equal(before, result.PreviousHash);
        Assert.NotEqual(before, result.Hash);
        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Equal("Reworded", repository.Git("log", "-1", "--format=%B").TrimEnd());
    }

    [Fact]
    public async Task GivenAnUnbornBranch_WhenCommitting_ThenTheFirstCommitIsMade()
    {
        using var repository = TestRepository.Create();
        repository.WriteFile("a.txt", "a\n");
        repository.Git("add", "a.txt");
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        var result = await writer.CommitAsync(location, new CommitRequest("First", false, false, false), Token);

        Assert.Equal(repository.Head, result.Hash);
        Assert.Equal("First", await writer.ReadHeadMessageAsync(location, Token));
    }

    [Fact]
    public async Task GivenNoCommit_WhenReadingTheHeadMessage_ThenItIsNull()
    {
        using var repository = TestRepository.Create();
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        Assert.Null(await writer.ReadHeadMessageAsync(location, Token));
    }

    [Fact]
    public async Task GivenACommit_WhenReadingTheHeadMessage_ThenTheWholeMessageComesBack()
    {
        using var repository = TestRepository.Create().Commit("Subject\n\nBody line", new TestFile("a.txt", "a\n"));
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        Assert.Equal("Subject\n\nBody line", await writer.ReadHeadMessageAsync(location, Token));
    }

    [Fact]
    public async Task GivenAFailingPreCommitHook_WhenCommitting_ThenItsOutputIsInTheErrorAndNothingIsCommitted()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        WriteHook(repository, "pre-commit", "echo 'lint: trailing whitespace in a.txt'\nexit 1\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var before = repository.Head;
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        var failure = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.CommitAsync(location, new CommitRequest("Blocked", false, false, false), Token));

        Assert.Contains("lint: trailing whitespace in a.txt", failure.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, repository.Head);
    }

    [Fact]
    public async Task GivenNothingStaged_WhenCommitting_ThenGitsStdoutIsInTheError()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        var failure = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.CommitAsync(location, new CommitRequest("Empty", false, false, false), Token));

        Assert.Contains("nothing to commit", failure.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFailingHookAndNoVerify_WhenCommitting_ThenTheHookIsSkipped()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        WriteHook(repository, "pre-commit", "exit 1\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        var result = await writer.CommitAsync(location, new CommitRequest("Skipped", false, true, false), Token);

        Assert.Equal(repository.Head, result.Hash);
    }

    [Fact]
    public async Task GivenAHookThatReadsInput_WhenCommitting_ThenItSeesAClosedStdinAndFailsInsteadOfWaiting()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        WriteHook(repository, "pre-commit", "if ! read -r answer; then echo 'hook wanted input' >&2; exit 1; fi\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        var failure = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.CommitAsync(location, new CommitRequest("Prompted", false, false, false), Token));

        Assert.Contains("hook wanted input", failure.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMergeWithConflicts_WhenReadingTheMergeMessage_ThenCommentLinesAreDropped()
    {
        using var repository = TestRepository.Create()
            .Commit("root", new TestFile("a.txt", "a\n"))
            .CheckoutNewBranch("other")
            .Commit("other", new TestFile("a.txt", "other\n"))
            .Checkout("main")
            .Commit("main", new TestFile("a.txt", "main\n"));
        repository.MergeExpectingConflict("other");
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        var message = await writer.ReadMergeMessageAsync(location, Token);

        Assert.NotNull(message);
        Assert.StartsWith("Merge", message, StringComparison.Ordinal);
        Assert.DoesNotContain('#', message);
    }

    [Fact]
    public async Task GivenNoMerge_WhenReadingTheMergeMessage_ThenItIsNull()
    {
        using var repository = TestRepository.Create().Commit("root", new TestFile("a.txt", "a\n"));
        var location = await LocateAsync(repository);
        var writer = new CommitWriter(repository.CreateIsolatedRunner());

        Assert.Null(await writer.ReadMergeMessageAsync(location, Token));
    }

    private static void WriteHook(TestRepository repository, string name, string script)
    {
        var path = Path.Combine(repository.WorkingTreePath, ".git", "hooks", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"#!/bin/sh\n{script}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
