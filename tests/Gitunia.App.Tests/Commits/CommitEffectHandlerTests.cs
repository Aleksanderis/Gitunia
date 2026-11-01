using Gitunia.App.Commits;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Commits;
using Gitunia.Core.Editing;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Commits;

// The handler is the only place that runs the commit and reads the messages: real git where git matters, a fake for the
// failures and the cancel.
public class CommitEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenAStagedFile_WhenCommitting_ThenTheCommitExistsAndTheResultCarriesIt()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var location = await LocateAsync(repository);
        var request = new CommitRequest("Change a", false, false, false);

        var result = await RunAsync(TestGit.Services.CommitWriter, new RunCommitEffect(Tab, location, request, null));

        var success = Assert.IsType<CommitSucceeded>(result);
        Assert.Equal(request, success.Request);
        Assert.Equal(repository.Head, success.Result.Hash);
        Assert.Equal("Change a", repository.Git("log", "-1", "--format=%B").Trim());
    }

    [Fact]
    public async Task GivenTheForcePushUpstream_WhenCommitting_ThenItTravelsWithTheResult()
    {
        var writer = new FakeCommitWriter();

        var result = await RunAsync(writer, new RunCommitEffect(Tab, default!, new CommitRequest("m", false, false, true), "origin/main"));

        Assert.Equal("origin/main", Assert.IsType<CommitSucceeded>(result).ForcePushUpstream);
    }

    [Fact]
    public async Task GivenAFailingHook_WhenCommitting_ThenTheResultHoldsTheNoticeAndTheOutput()
    {
        var writer = new FakeCommitWriter
        {
            Failure = new GitCommandFailedException(["commit"], 1, "lint failed: a.txt\nfix it\n"),
        };

        var result = await RunAsync(writer, new RunCommitEffect(Tab, default!, new CommitRequest("m", false, false, false), null));

        var failed = Assert.IsType<CommitFailed>(result);
        Assert.Equal("Commit failed: lint failed: a.txt", failed.Notice);
        Assert.Contains("lint failed: a.txt", failed.Output!);
        Assert.Contains("fix it", failed.Output!);
        Assert.Contains("<space>ec", failed.Output![^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenGitPrintingOnStdout_WhenCommitting_ThenTheNoticeAndTheOutputIncludeIt()
    {
        var writer = new FakeCommitWriter
        {
            Failure = new GitCommandFailedException(["commit"], 1, string.Empty, "nothing to commit, working tree clean\n"),
        };

        var result = await RunAsync(writer, new RunCommitEffect(Tab, default!, new CommitRequest("m", false, false, false), null));

        var failed = Assert.IsType<CommitFailed>(result);
        Assert.Equal("Commit failed: nothing to commit, working tree clean", failed.Notice);
        Assert.Equal("nothing to commit, working tree clean", failed.Output![0]);
    }

    [Fact]
    public async Task GivenAHookThatWantsInput_WhenCommitting_ThenTheEmptyOutputStillGetsTheHint()
    {
        var writer = new FakeCommitWriter { Failure = new GitCommandFailedException(["commit"], 1, string.Empty) };

        var result = await RunAsync(writer, new RunCommitEffect(Tab, default!, new CommitRequest("m", false, false, false), null));

        var failed = Assert.IsType<CommitFailed>(result);
        Assert.Equal(CommitFailureReport.NoOutput, failed.Output![0]);
        Assert.Contains("passphrase", failed.Output[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnotherFailure_WhenCommitting_ThenTheNoticeHasItsMessageAndThereIsNoOutput()
    {
        var writer = new FakeCommitWriter { Failure = new InvalidOperationException("disk gone") };

        var result = await RunAsync(writer, new RunCommitEffect(Tab, default!, new CommitRequest("m", false, false, false), null));

        var failed = Assert.IsType<CommitFailed>(result);
        Assert.Equal("Commit failed: disk gone", failed.Notice);
        Assert.Null(failed.Output);
    }

    [Fact]
    public async Task GivenARunningCommit_WhenItIsCancelled_ThenTheResultIsTheCancelNotice()
    {
        var writer = new FakeCommitWriter { IsHeld = true };
        var dispatcher = new RecordingDispatcher();
        var handler = new CommitEffectHandler(writer, dispatcher);
        handler.Handle(
            new RunCommitEffect(Tab, default!, new CommitRequest("m", false, false, false), null),
            TestContext.Current.CancellationToken);
        await Until(() => writer.Requests.Count == 1);

        handler.Cancel(Tab);

        var failed = Assert.IsType<CommitFailed>(await dispatcher.NextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(CommitTexts.CancelledNotice, failed.Notice);
        Assert.Null(failed.Output);
    }

    [Fact]
    public async Task GivenACancelledTab_WhenCommitting_ThenTheResultIsTheCancelNotice()
    {
        var writer = new FakeCommitWriter { IsHeld = true };
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var dispatcher = new RecordingDispatcher();

        new CommitEffectHandler(writer, dispatcher)
            .Handle(new RunCommitEffect(Tab, default!, new CommitRequest("m", false, false, false), null), cancelled.Token);

        var failed = Assert.IsType<CommitFailed>(await dispatcher.NextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(CommitTexts.CancelledNotice, failed.Notice);
    }

    [Fact]
    public void GivenNoCommitRuns_WhenCancelling_ThenNothingHappens()
    {
        new CommitEffectHandler(new FakeCommitWriter(), new RecordingDispatcher()).Cancel(Tab);
    }

    [Fact]
    public async Task GivenACommit_WhenReadingHeadsMessage_ThenTheMessageComesBackWithTheHash()
    {
        using var repository = TestRepository.Create().Commit("Initial subject", new TestFile("a.txt", "a\n"));
        var location = await LocateAsync(repository);

        var result = await ReadAsync(TestGit.Services.CommitWriter, new ReadHeadMessageEffect(Tab, location, "abc"));

        var read = Assert.IsType<HeadMessageRead>(result);
        Assert.Equal("abc", read.Hash);
        Assert.Equal("Initial subject", read.Message.Trim());
    }

    [Fact]
    public async Task GivenHeadsMessageCannotBeRead_WhenReadingIt_ThenTheFailureCarriesTheReason()
    {
        var writer = new ThrowingWriter();

        var result = await ReadAsync(writer, new ReadHeadMessageEffect(Tab, default!, null));

        Assert.Equal("boom", Assert.IsType<HeadMessageReadFailed>(result).Notice);
    }

    [Fact]
    public async Task GivenAPreparedMergeMessage_WhenReadingIt_ThenItComesBack()
    {
        var writer = new FakeCommitWriter { MergeMessage = "Merge branch 'other'" };

        var result = await ReadAsync(writer, new ReadMergeMessageEffect(Tab, default!));

        Assert.Equal("Merge branch 'other'", Assert.IsType<MergeMessageRead>(result).Message);
    }

    [Fact]
    public async Task GivenNoMergeMessage_WhenReadingIt_ThenNothingIsPosted()
    {
        var writer = new FakeCommitWriter();
        var dispatcher = new RecordingDispatcher();
        new CommitEffectHandler(writer, dispatcher)
            .Handle(new ReadMergeMessageEffect(Tab, default!), TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            guard.CancelAfter(TimeSpan.FromMilliseconds(200));
            await dispatcher.NextAsync(guard.Token);
        });
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunAsync(ICommitWriter writer, RunCommitEffect effect)
    {
        var dispatcher = new RecordingDispatcher();
        new CommitEffectHandler(writer, dispatcher).Handle(effect, TestContext.Current.CancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<AppCommand> ReadAsync(ICommitWriter writer, CommitReadEffect effect)
    {
        var dispatcher = new RecordingDispatcher();
        new CommitEffectHandler(writer, dispatcher).Handle(effect, TestContext.Current.CancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private sealed class ThrowingWriter : ICommitWriter
    {
        public Task<CommitResult> CommitAsync(
            RepositoryLocation location,
            CommitRequest request,
            CancellationToken cancellationToken) => throw new InvalidOperationException("boom");

        public Task<string?> ReadHeadMessageAsync(RepositoryLocation location, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");

        public Task<string?> ReadMergeMessageAsync(RepositoryLocation location, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
