using Gitunia.App.Sync;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Sync;

// The handler is the only place that runs fetch, pull, push and the remote delete: real git on temporary repositories
// for what git does, a fake writer for what cannot be provoked (credentials, progress, a cancel mid-run).
public class SyncEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenARemote_WhenFetching_ThenTheRemoteOfTheBranchIsFetched()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var theirs = PushFromAnotherClone(repository, "topic");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new FetchEffect(Tab, location, false));

        Assert.Equal("Fetched the remote of the current branch", Assert.IsType<SyncSucceeded>(result).Text);
        Assert.Equal(theirs, repository.Git("rev-parse", "origin/topic").Trim());
    }

    [Fact]
    public async Task GivenFetchAll_WhenFetching_ThenEveryRemoteIsRequested()
    {
        var writer = new FakeRemoteWriter();
        var location = FakeLocation();

        var result = await RunAsync(new FetchEffect(Tab, location, true), writer);

        Assert.Equal("Fetched all remotes", Assert.IsType<SyncSucceeded>(result).Text);
        Assert.Equal([$"fetch|{location.RootPath}|all=True|prune=True"], writer.Calls);
    }

    [Fact]
    public async Task GivenNothingNew_WhenPulling_ThenItIsAlreadyUpToDate()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PullEffect(Tab, location));

        Assert.Equal("Already up to date", Assert.IsType<SyncSucceeded>(result).Text);
    }

    [Fact]
    public async Task GivenNewCommitsOnTheRemote_WhenPulling_ThenTheBranchFastForwards()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var theirs = PushFromAnotherClone(repository, "topic");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PullEffect(Tab, location));

        Assert.Equal("Pulled (fast-forward)", Assert.IsType<SyncSucceeded>(result).Text);
        Assert.Equal(theirs, repository.Git("rev-parse", "HEAD").Trim());
    }

    [Fact]
    public async Task GivenADivergedPull_WhenItFails_ThenTheErrorSaysSo()
    {
        var writer = new FakeRemoteWriter { Failure = new PullDivergedException("fatal: Not possible to fast-forward") };

        var result = await RunAsync(new PullEffect(Tab, FakeLocation()), writer);

        var failure = Assert.IsType<SyncFailed>(result);
        Assert.StartsWith(
            "Pull failed: Cannot fast-forward: local and remote have diverged",
            failure.Text,
            StringComparison.Ordinal);
        Assert.Null(failure.Retry);
    }

    [Fact]
    public async Task GivenAPush_WhenGitRefuses_ThenTheErrorShowsGitsFirstLineAndNoForceIsOffered()
    {
        var failure = new GitCommandFailedException(["push"], 1, "! [rejected] main -> main\nhint: more");
        var writer = new FakeRemoteWriter { Failure = failure };
        var location = FakeLocation();

        var result = await RunAsync(new PushEffect(Tab, location, new PushRequest(null), "Pushed", "main"), writer);

        var failed = Assert.IsType<SyncFailed>(result);
        Assert.Equal("Push failed: ! [rejected] main -> main", failed.Text);
        Assert.Null(failed.Rejection);
        Assert.Equal(
            [$"push|{location.RootPath}|remote=(default)|branch=|upstream=False|lease=:"],
            writer.Calls);
    }

    [Fact]
    public async Task GivenARewrittenBranch_WhenPushingPlainly_ThenTheRejectionNamesTheBranchForTheForceOffer()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PushEffect(Tab, location, new PushRequest(null), "Pushed", "topic"));

        var failed = Assert.IsType<SyncFailed>(result);
        Assert.StartsWith("Push failed", failed.Text, StringComparison.Ordinal);
        Assert.Equal(new SyncRejection("topic", null), failed.Rejection);
        Assert.Null(failed.Retry);
    }

    [Fact]
    public async Task GivenARemoteBranchOfTheSameName_WhenPushingAFirstTime_ThenTheRejectionNamesTheRemote()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        PushFromAnotherClone(repository, "lonely", createBranch: true);
        repository.Git("fetch", "-q", "origin");
        var location = await LocateAsync(repository);

        var result = await RunAsync(
            new PushEffect(Tab, location, new PushRequest("origin", "lonely", true), "Pushed", "lonely"));

        Assert.Equal(new SyncRejection("lonely", "origin"), Assert.IsType<SyncFailed>(result).Rejection);
    }

    [Fact]
    public async Task GivenTheRemoteMovedMeanwhile_WhenForcePushing_ThenTheLeaseIsRejectedWithoutAnotherForceOffer()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        var theirs = PushFromAnotherClone(repository, "topic");
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");
        var location = await LocateAsync(repository);
        var request = new PushRequest("origin", "topic", false, new PushLease("topic", seen));

        var result = await RunAsync(new PushEffect(Tab, location, request, "Force pushed", "topic"));

        var failed = Assert.IsType<SyncFailed>(result);
        Assert.StartsWith("Push failed", failed.Text, StringComparison.Ordinal);
        Assert.Null(failed.Rejection);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenABranchWithoutUpstream_WhenPushingItToARemote_ThenTheUpstreamIsSet()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        var location = await LocateAsync(repository);
        var request = new PushRequest("origin", "lonely", true);

        var result = await RunAsync(new PushEffect(Tab, location, request, "Pushed lonely", "lonely"));

        Assert.Equal("Pushed lonely", Assert.IsType<SyncSucceeded>(result).Text);
        Assert.Equal("origin/lonely", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
        Assert.Equal(repository.Head, repository.GitIn(repository.OriginPath, ["rev-parse", "lonely"]).Trim());
    }

    [Theory]
    [InlineData("user@host: Permission denied (publickey).\nfatal: Could not read from remote repository.")]
    [InlineData("fatal: could not read Username for 'https://example.com': terminal prompts disabled")]
    public async Task GivenAFetchThatNeedsCredentials_WhenItFails_ThenTheTerminalRetryHasTheSameFetch(
        string standardError)
    {
        var writer = new FakeRemoteWriter { Failure = new GitCommandFailedException(["fetch"], 128, standardError) };

        var result = await RunAsync(new FetchEffect(Tab, FakeLocation(), false), writer);

        var retry = Assert.IsType<SyncFailed>(result).Retry!;
        Assert.Equal("Fetch", retry.Noun);
        Assert.Equal(["fetch", "--prune"], retry.Arguments);
    }

    [Fact]
    public async Task GivenAPullThatNeedsCredentials_WhenItFails_ThenTheTerminalRetryIsAFastForwardOnlyPull()
    {
        var failure = new GitCommandFailedException(["fetch"], 128, "fatal: Authentication failed for 'x'");
        var writer = new FakeRemoteWriter { Failure = failure };

        var result = await RunAsync(new PullEffect(Tab, FakeLocation()), writer);

        var retry = Assert.IsType<SyncFailed>(result).Retry!;
        Assert.Equal("Pull", retry.Noun);
        Assert.Equal("pull --no-rebase --ff-only", string.Join(' ', retry.Arguments));
    }

    [Fact]
    public async Task GivenAForcePushThatNeedsCredentials_WhenItFails_ThenTheTerminalRetryKeepsTheLease()
    {
        var failure = new GitCommandFailedException(["push"], 128, "Permission denied (publickey).");
        var writer = new FakeRemoteWriter { Failure = failure };
        var request = new PushRequest("origin", "topic", false, new PushLease("topic", "abc123"));

        var result = await RunAsync(new PushEffect(Tab, FakeLocation(), request, "Force pushed", "topic"), writer);

        var retry = Assert.IsType<SyncFailed>(result).Retry!;
        Assert.Equal(
            "push --force-with-lease=refs/heads/topic:abc123 -- origin topic:refs/heads/topic",
            string.Join(' ', retry.Arguments));
    }

    [Fact]
    public async Task GivenAFailureThatIsNotAboutCredentials_WhenItFails_ThenNoTerminalRetryIsOffered()
    {
        var failure = new GitCommandFailedException(["fetch"], 128, "fatal: repository 'x' not found");
        var writer = new FakeRemoteWriter { Failure = failure };

        var result = await RunAsync(new FetchEffect(Tab, FakeLocation(), false), writer);

        Assert.Null(Assert.IsType<SyncFailed>(result).Retry);
    }

    [Fact]
    public async Task GivenARemoteBranchDeleteThatNeedsCredentials_WhenItFails_ThenTheTerminalRetryRunsTheSameDelete()
    {
        var failure = new GitCommandFailedException(["push"], 128, "git@host: Permission denied (publickey).");
        var writer = new FakeRemoteWriter { Failure = failure };

        var result = await RunAsync(new DeleteRemoteBranchEffect(Tab, FakeLocation(), "origin", "topic", "abc"), writer);

        var failed = Assert.IsType<SyncFailed>(result);
        Assert.Equal("Could not delete the branch on the remote: git@host: Permission denied (publickey).", failed.Text);
        Assert.Equal("Remote branch delete", failed.Retry!.Noun);
        Assert.Equal(
            "push --delete --force-with-lease=refs/heads/topic:abc -- origin refs/heads/topic",
            string.Join(' ', failed.Retry.Arguments));
    }

    [Fact]
    public async Task GivenARemoteBranchDeleteThatFailsOtherwise_WhenItFails_ThenNoTerminalRetryIsOffered()
    {
        var failure = new GitCommandFailedException(["push"], 1, "! [remote rejected] topic (stale info)");
        var writer = new FakeRemoteWriter { Failure = failure };

        var result = await RunAsync(new DeleteRemoteBranchEffect(Tab, FakeLocation(), "origin", "topic", "abc"), writer);

        Assert.Null(Assert.IsType<SyncFailed>(result).Retry);
    }

    [Fact]
    public async Task GivenARemoteBranch_WhenDeletingIt_ThenItIsGoneOnTheRemoteAndTheTextSaysHowToRestoreIt()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var tip = repository.Git("rev-parse", "origin/topic").Trim();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new DeleteRemoteBranchEffect(Tab, location, "origin", "topic", tip));

        var success = Assert.IsType<SyncSucceeded>(result);
        Assert.Equal($"Deleted topic on origin (was {tip[..7]})", success.Text);
        Assert.Equal($"Restore: git push origin {tip}:refs/heads/topic", success.Detail);
        Assert.DoesNotContain("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
        Assert.Equal(tip, repository.Git("rev-parse", "topic").Trim());
    }

    [Fact]
    public async Task GivenTheRemoteBranchMovedMeanwhile_WhenDeletingIt_ThenTheLeaseKeepsTheirCommit()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        var theirs = PushFromAnotherClone(repository, "topic");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new DeleteRemoteBranchEffect(Tab, location, "origin", "topic", seen));

        Assert.StartsWith("Could not delete the branch on the remote:", Assert.IsType<SyncFailed>(result).Text);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenTwoRemotes_WhenListing_ThenBothNamesComeBackWithTheRequest()
    {
        using var repository = TestRepositories.WithOrigin();
        repository.Git("remote", "add", "backup", repository.OriginPath);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ListSyncRemotesEffect(Tab, location, SyncRemotesPurpose.Delete, null, "origin/x"));

        var listed = Assert.IsType<SyncRemotesListed>(result);
        Assert.Equal(["backup", "origin"], listed.Remotes.Order(StringComparer.Ordinal));
        Assert.Equal((SyncRemotesPurpose.Delete, "origin/x"), (listed.Purpose, listed.DirectName));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenTwoTips_WhenComparing_ThenTheResultSaysWhetherTheLocalOneIsInTheRemotesHistory(bool behind)
    {
        using var repository = TestRepositories.Linear();
        var older = repository.Git("rev-parse", "HEAD~1").Trim();
        var location = await LocateAsync(repository);
        var (local, tracking) = behind ? (older, repository.Head) : (repository.Head, older);

        var result = await RunAsync(new CheckBehindEffect(Tab, location, "main", local, "origin", tracking));

        var checkedBehind = Assert.IsType<SyncBehindChecked>(result);
        Assert.Equal(behind, checkedBehind.IsBehind);
        Assert.Equal((local, tracking), (checkedBehind.CommitHash, checkedBehind.TrackingHash));
    }

    [Fact]
    public async Task GivenProgress_WhenRunning_ThenEachReportComesBeforeTheResult()
    {
        var writer = new FakeRemoteWriter();
        writer.Reports.Add(new RemoteProgress("Receiving objects", 45));
        var dispatcher = new RecordingDispatcher();

        new SyncEffectHandler(writer, TestGit.Services.RemoteManager, TestGit.Services.Ancestry, dispatcher)
            .Handle(new FetchEffect(Tab, FakeLocation(), false), TestContext.Current.CancellationToken);

        var progress = Assert.IsType<SyncProgressed>(await dispatcher.NextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new RemoteProgress("Receiving objects", 45), progress.Report);
        Assert.Equal(Tab, progress.TabId);
        Assert.IsType<SyncSucceeded>(await dispatcher.NextAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenARunningFetch_WhenCancelling_ThenGitIsStoppedForThatTabOnlyAndTheResultIsCancelled()
    {
        var writer = new FakeRemoteWriter { Blocks = true };
        var dispatcher = new RecordingDispatcher();
        var handler = new SyncEffectHandler(writer, TestGit.Services.RemoteManager, TestGit.Services.Ancestry, dispatcher);
        handler.Handle(new FetchEffect(Tab, FakeLocation(), false), TestContext.Current.CancellationToken);
        await writer.Started;

        handler.Cancel(new TabId(2));
        handler.Cancel(Tab);

        Assert.Equal(Tab, ((SyncCancelled)await dispatcher.NextAsync(TestContext.Current.CancellationToken)).TabId);
    }

    [Fact]
    public async Task GivenNothingRuns_WhenCancelling_ThenNothingHappens()
    {
        var dispatcher = new RecordingDispatcher();
        var handler = new SyncEffectHandler(
            new FakeRemoteWriter(),
            TestGit.Services.RemoteManager,
            TestGit.Services.Ancestry,
            dispatcher);

        handler.Cancel(Tab);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.NextAsync(new CancellationToken(true)));
    }

    [Fact]
    public async Task GivenACancelledTab_WhenFetching_ThenTheResultIsCancelled()
    {
        var writer = new FakeRemoteWriter { Blocks = true };
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await RunWithTokenAsync(new FetchEffect(Tab, FakeLocation(), false), writer, cancelled.Token);

        Assert.IsType<SyncCancelled>(result);
    }

    private static RepositoryLocation FakeLocation()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-sync-handler-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static Task<AppCommand> RunAsync(SyncEffect effect, IRemoteWriter? writer = null) =>
        RunWithTokenAsync(effect, writer, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunWithTokenAsync(
        SyncEffect effect,
        IRemoteWriter? writer,
        CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        var handler = new SyncEffectHandler(
            writer ?? TestGit.Services.Remote,
            TestGit.Services.RemoteManager,
            TestGit.Services.Ancestry,
            dispatcher);
        handler.Handle(effect, cancellationToken);

        // Progress reports come first; the result is the one that ends the operation.
        while (await dispatcher.NextAsync(TestContext.Current.CancellationToken) is { } posted)
        {
            if (posted is not SyncProgressed)
            {
                return posted;
            }
        }

        throw new InvalidOperationException("No result was posted");
    }

    private static string PushFromAnotherClone(TestRepository repository, string branch, bool createBranch = false)
    {
        var root = Path.GetDirectoryName(repository.OriginPath)!;
        var clone = Path.Combine(root, "other-clone");
        if (createBranch)
        {
            repository.GitIn(root, ["clone", "-q", repository.OriginPath, clone]);
            repository.GitIn(clone, ["checkout", "-q", "-b", branch]);
        }
        else
        {
            repository.GitIn(root, ["clone", "-q", "--branch", branch, repository.OriginPath, clone]);
        }

        File.WriteAllText(Path.Combine(clone, "theirs.txt"), "t\n");
        repository.GitIn(clone, ["add", "-A"]);
        repository.GitIn(clone, ["commit", "-q", "-m", "Theirs"]);
        repository.GitIn(clone, ["push", "-q", "origin", branch]);

        return repository.GitIn(clone, ["rev-parse", "HEAD"]).Trim();
    }
}
