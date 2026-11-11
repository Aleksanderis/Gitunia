using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.App.Tests.Fakes;
using Gitunia.App.Worktrees;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Worktrees;

public sealed class WorktreeOverviewPollerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenCleanDirtyAndPrunableWorktrees_WhenPolled_ThenOverviewMatchesStatusOfEach()
    {
        using var repository = TestRepositories.WithManyWorktrees(
            out var lockedPath,
            out var prunablePath,
            out var dirtyPath);
        repository.WriteFile("main-new.txt", "x");
        var host = await OpenAsync(repository.WorkingTreePath);
        using var poller = new WorktreeOverviewPoller(TestGit.Services.Status, host);

        await poller.PollOnceAsync(Token);

        var statuses = host.Workspace.ActiveTab!.WorktreeStatuses!;
        var dirty = statuses[dirtyPath];
        var porcelain = repository.GitIn(dirtyPath, ["status", "--porcelain", "--untracked-files=all"]);
        Assert.Equal(
            porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length,
            dirty.Staged + dirty.Changed + dirty.Conflicted);
        Assert.True(dirty.IsDirty);
        Assert.False(statuses[lockedPath].IsDirty);
        Assert.False(statuses.ContainsKey(prunablePath));
        Assert.False(statuses.ContainsKey(repository.WorkingTreePath));
    }

    [Fact]
    public async Task GivenPolledOverview_WhenAWorktreeBecomesDirtyAndPolledAgain_ThenTheRowFollows()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var host = await OpenAsync(repository.WorkingTreePath);
        using var poller = new WorktreeOverviewPoller(TestGit.Services.Status, host);
        await poller.PollOnceAsync(Token);
        Assert.False(host.Workspace.ActiveTab!.WorktreeStatuses![path].IsDirty);

        await File.WriteAllTextAsync(Path.Combine(path, "fresh.txt"), "x", Token);
        await poller.PollOnceAsync(Token);

        Assert.Equal(1, host.Workspace.ActiveTab!.WorktreeStatuses![path].Changed);
    }

    [Fact]
    public async Task GivenSingleWorktree_WhenPolled_ThenNothingIsDispatched()
    {
        using var repository = TestRepositories.Linear(2);
        var host = await OpenAsync(repository.WorkingTreePath);
        using var poller = new WorktreeOverviewPoller(TestGit.Services.Status, host);

        await poller.PollOnceAsync(Token);

        Assert.Null(host.Workspace.ActiveTab!.WorktreeStatuses);
    }

    [Fact]
    public async Task GivenWorktreeThatKeepsFailing_WhenPolledRepeatedly_ThenItIsSkippedForLongerEachTime()
    {
        using var repository = TestRepositories.WithWorktree(out _);
        var host = await OpenAsync(repository.WorkingTreePath);
        var reader = new FailingStatusReader();
        var clock = new ManualClock();
        using var poller = new WorktreeOverviewPoller(reader, host, time: clock);
        var step = TimeSpan.FromSeconds(30);
        var gaps = new List<TimeSpan>();

        for (var failure = 0; failure < 3; failure++)
        {
            await poller.PollOnceAsync(Token);
            var reads = reader.Reads;
            var gap = TimeSpan.Zero;
            while (reader.Reads == reads)
            {
                clock.Advance(step);
                gap += step;
                await poller.PollOnceAsync(Token);
            }

            gaps.Add(gap);
        }

        Assert.True(gaps[0] >= step);
        Assert.True(gaps[1] > gaps[0]);
        Assert.True(gaps[2] > gaps[1]);
    }

    private sealed class FailingStatusReader : IStatusReader
    {
        public int Reads { get; private set; }

        public Task<StatusSnapshot> ReadStatusAsync(
            RepositoryLocation location,
            bool includeIgnored,
            UntrackedFilesMode untrackedFiles,
            CancellationToken cancellationToken)
        {
            Reads++;

            return Task.FromException<StatusSnapshot>(new TimeoutException("status timed out"));
        }
    }

    private static async Task<Dispatcher> OpenAsync(string path)
    {
        var host = new Dispatcher(AppState.Empty);
        var location = await TestGit.Services.Discovery.DiscoverAsync(path, Token);
        host.Dispatch(new OpenRepositoryCommand(location));
        using var session = new FakeGraphSession(host.Workspace.ActiveTabId!.Value);
        await TestGit.Services.RepositoryLoader
            .RunAsync(location, session, host, new StatusReporter(host), InlineUi.Instance, Token);
        return host;
    }
}
