using System.Diagnostics;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Remotes;

public class RemoteWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenANewRemoteCommit_WhenFetching_ThenTheTrackingBranchMovesAndProgressIsReported()
    {
        using var repository = TestRepositories.WithOrigin();
        var remoteTip = CommitOnOriginWithoutFetching(repository);
        var progress = new CollectingProgress();

        await WriterFor(repository).FetchAsync(
            await LocateAsync(repository),
            new FetchRequest("origin", false, false),
            progress,
            Token);

        Assert.Equal(remoteTip, repository.Git("rev-parse", "origin/main").Trim());
        Assert.Contains(progress.Reports, report => report.Percent == 100);
    }

    [Fact]
    public async Task GivenABranchDeletedOnTheRemote_WhenFetchingWithPrune_ThenTheTrackingBranchIsRemoved()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        repository.GitIn(repository.OriginPath, ["branch", "-D", "topic"]);
        var location = await LocateAsync(repository);
        var writer = WriterFor(repository);

        await writer.FetchAsync(location, new FetchRequest("origin", false, false), null, Token);
        Assert.Contains("origin/topic", repository.Git("branch", "-r"), StringComparison.Ordinal);

        await writer.FetchAsync(location, new FetchRequest("origin", false, true), null, Token);
        Assert.DoesNotContain("origin/topic", repository.Git("branch", "-r"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTwoRemotes_WhenFetchingAll_ThenBothAreUpdated()
    {
        using var repository = TestRepositories.WithOrigin();
        var second = Path.Combine(Path.GetDirectoryName(repository.OriginPath)!, "second.git");
        repository.GitIn(
            Path.GetDirectoryName(repository.OriginPath)!,
            ["clone", "-q", "--bare", repository.OriginPath, second]);
        repository.Git("remote", "add", "second", second);

        await WriterFor(repository).FetchAsync(
            await LocateAsync(repository),
            new FetchRequest(null, true, false),
            null,
            Token);

        Assert.Contains("second/main", repository.Git("branch", "-r"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARemoteAheadOfTheBranch_WhenPulling_ThenHeadFastForwards()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 2);
        var remoteTip = repository.Git("rev-parse", "origin/topic").Trim();

        var outcome = await WriterFor(repository).PullAsync(await LocateAsync(repository), null, Token);

        Assert.Equal(PullOutcome.FastForwarded, outcome);
        Assert.Equal(remoteTip, repository.Head);
    }

    [Fact]
    public async Task GivenNothingNew_WhenPulling_ThenTheOutcomeIsUpToDate()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);

        var outcome = await WriterFor(repository).PullAsync(await LocateAsync(repository), null, Token);

        Assert.Equal(PullOutcome.UpToDate, outcome);
    }

    [Fact]
    public async Task GivenDivergedBranches_WhenPulling_ThenItFailsAndChangesNothing()
    {
        using var repository = TestRepositories.WithTrackingBranch(1, 1);
        var head = repository.Head;
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<PullDivergedException>(() => WriterFor(repository).PullAsync(location, null, Token));

        Assert.Equal(head, repository.Head);
        Assert.Equal(string.Empty, repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task GivenDivergedBranchesAndPullRebaseConfig_WhenPulling_ThenItStillDoesNotRebase()
    {
        using var repository = TestRepositories.WithTrackingBranch(1, 1);
        repository.Git("config", "pull.rebase", "true");
        var head = repository.Head;
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<PullDivergedException>(() => WriterFor(repository).PullAsync(location, null, Token));

        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenALocalCommit_WhenPushing_ThenTheRemoteBranchHasIt()
    {
        using var repository = TestRepositories.WithTrackingBranch(1, 0);
        var head = repository.Head;

        await WriterFor(repository).PushAsync(await LocateAsync(repository), new PushRequest(null), null, Token);

        Assert.Equal(head, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenAnUnknownRemote_WhenPushing_ThenGitsErrorIsThrown()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).PushAsync(location, new PushRequest("nowhere"), null, Token));
    }

    [Fact]
    public async Task GivenABranchWithoutUpstream_WhenPushingWithSetUpstream_ThenTheRemoteHasItAndItTracksIt()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        var head = repository.Head;
        Assert.Equal(string.Empty, repository.Git(["config", "--get", "branch.lonely.remote"], [0, 1]).Trim());

        await WriterFor(repository).PushAsync(
            await LocateAsync(repository),
            new PushRequest("origin", "lonely", true),
            null,
            Token);

        Assert.Equal(head, repository.GitIn(repository.OriginPath, ["rev-parse", "lonely"]).Trim());
        Assert.Equal("origin/lonely", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
    }

    [Fact]
    public async Task GivenARewrittenBranch_WhenPushingWithTheLeaseOfTheTrackingBranch_ThenTheRemoteIsOverwritten()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");
        var rewritten = repository.Head;
        Assert.NotEqual(seen, rewritten);

        await WriterFor(repository).PushAsync(
            await LocateAsync(repository),
            new PushRequest("origin", "topic", false, new PushLease("topic", seen)),
            null,
            Token);

        Assert.Equal(rewritten, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenTheRemoteMoved_WhenForcePushingWithALease_ThenItIsRejectedAndNothingChanges()
    {
        using var repository = TestRepositories.WithOrigin();
        var seen = repository.Git("rev-parse", "origin/main").Trim();
        var theirs = CommitOnOriginWithoutFetching(repository);
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");
        var location = await LocateAsync(repository);

        var failure = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).PushAsync(
                location,
                new PushRequest("origin", "main", false, new PushLease("main", seen)),
                null,
                Token));

        Assert.Contains("stale info", failure.StandardError, StringComparison.Ordinal);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "main"]).Trim());
    }

    [Fact]
    public async Task GivenABranchOnTheRemote_WhenDeletingIt_ThenItIsGoneThereAndTheLocalBranchStays()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var head = repository.Head;

        await WriterFor(repository).DeleteRemoteBranchAsync(
            await LocateAsync(repository),
            "origin",
            "topic",
            repository.Git("rev-parse", "origin/topic").Trim(),
            Token);

        Assert.DoesNotContain("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
        Assert.DoesNotContain("origin/topic", repository.Git("branch", "-r"), StringComparison.Ordinal);
        Assert.Equal(head, repository.Git("rev-parse", "topic").Trim());
    }

    [Fact]
    public async Task GivenTheBranchMovedOnTheRemote_WhenDeletingIt_ThenTheLeaseRejectsAndTheirCommitSurvives()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        var theirs = CommitOnOriginWithoutFetching(repository, "topic");
        var location = await LocateAsync(repository);

        var failure = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).DeleteRemoteBranchAsync(location, "origin", "topic", seen, Token));

        Assert.Contains("stale info", failure.StandardError, StringComparison.Ordinal);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenAnUnknownRemote_WhenDeletingABranch_ThenGitsErrorIsThrown()
    {
        using var repository = TestRepositories.WithOrigin();
        var head = repository.Head;
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).DeleteRemoteBranchAsync(location, "nowhere", "topic", head, Token));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenFetching_ThenGitNeverRuns()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => WriterFor(repository)
                .FetchAsync(location, new FetchRequest("origin", false, false), null, source.Token));
    }

    [Fact]
    public async Task GivenASlowRemote_WhenCancellingTheFetch_ThenItStopsAndTheRepositoryIsConsistent()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The slow remote is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        var before = repository.Git("rev-parse", "origin/main").Trim();
        CommitOnOriginWithoutFetching(repository);
        var slowSsh = Path.Combine(Path.GetDirectoryName(repository.OriginPath)!, "ssh");

        // Stands in for ssh and serves only the first bytes of the real upload-pack, then hangs mid-transfer.
        await WriteScriptAsync(
            slowSsh,
            $"#!/bin/sh\ngit upload-pack '{repository.OriginPath}' | (head -c 300; sleep 30)\n");
        repository.Git("remote", "add", "slow", "ssh://example.invalid/repo.git");
        var runner = repository.CreateIsolatedRunner();
        var writer = new RemoteWriter(new RemoteRunner(runner, name => name == "GIT_SSH_COMMAND" ? slowSsh : null));
        var location = await LocateAsync(repository);
        using var source = new CancellationTokenSource();

        var fetch = writer.FetchAsync(location, new FetchRequest("slow", false, false), null, source.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(700), Token);
        await source.CancelAsync();
        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Cancel must not wait for the 30 s hang.");
        Assert.Equal(before, repository.Git("rev-parse", "origin/main").Trim());
        Assert.Equal(string.Empty, repository.Git("fsck", "--no-progress").Trim());
        var gitDirectory = Path.Combine(repository.WorkingTreePath, ".git");
        Assert.Empty(Directory.GetFiles(gitDirectory, "*.lock", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task GivenAnSshRemote_WhenFetching_ThenSshIsStartedInBatchMode()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake ssh is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        var directory = Path.Combine(Path.GetTempPath(), $"gitunia-ssh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var log = Path.Combine(directory, "args.log");
            var script = Path.Combine(directory, "ssh");
            await WriteScriptAsync(script, $"#!/bin/sh\necho \"$@\" > '{log}'\nexit 255\n");
            repository.Git("remote", "add", "remote-ssh", "ssh://example.invalid/repo.git");
            var runner = repository.CreateIsolatedRunner();
            var writer = new RemoteWriter(
                new RemoteRunner(runner, name => name == "GIT_SSH_COMMAND" ? script : null));
            var location = await LocateAsync(repository);

            await Assert.ThrowsAsync<GitCommandFailedException>(
                () => writer.FetchAsync(location, new FetchRequest("remote-ssh", false, false), null, Token));

            Assert.Contains("BatchMode=yes", await File.ReadAllTextAsync(log, Token), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    // The new commit's objects exist only on the remote, so a fetch has to transfer a pack.
    private static string CommitOnOriginWithoutFetching(TestRepository repository, string branch = "main")
    {
        var root = Path.GetDirectoryName(repository.OriginPath)!;
        var clone = Path.Combine(root, "other-clone");
        repository.GitIn(root, ["clone", "-q", "--branch", branch, repository.OriginPath, clone]);
        File.WriteAllText(Path.Combine(clone, "remote.txt"), "r\n");
        repository.GitIn(clone, ["add", "-A"]);
        repository.GitIn(clone, ["commit", "-q", "-m", "Remote only"]);
        repository.GitIn(clone, ["push", "-q", "origin", branch]);
        return repository.GitIn(clone, ["rev-parse", "HEAD"]).Trim();
    }

    private static async Task WriteScriptAsync(string path, string content)
    {
        await File.WriteAllTextAsync(path, content, Token);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await new RepositoryDiscovery(repository.CreateIsolatedRunner())
            .DiscoverAsync(repository.WorkingTreePath, Token);

    private static RemoteWriter WriterFor(TestRepository repository) =>
        new(new RemoteRunner(repository.CreateIsolatedRunner(), _ => null));

    private sealed class CollectingProgress : IProgress<RemoteProgress>
    {
        private readonly List<RemoteProgress> _reports = [];

        public IReadOnlyList<RemoteProgress> Reports
        {
            get
            {
                lock (_reports)
                {
                    return [.. _reports];
                }
            }
        }

        public void Report(RemoteProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }
        }
    }
}
