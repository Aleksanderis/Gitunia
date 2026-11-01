using System.Collections.Concurrent;
using Gitunia.App.Tabs;
using Gitunia.App.Tabs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.App.Tests.Tabs;

public class TabPollerTests
{
    private const RepositoryChangeKinds StatusOnly = RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree;

    private static readonly PollOptions Fast = new(
        TimeSpan.FromMilliseconds(30),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromSeconds(1),
        5);

    // Only the check made when a tab is shown (and CheckNow) happens within a test; the next tick is an hour away.
    private static readonly PollOptions Slow = new(
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(1),
        5);

    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(400);

    [Fact]
    public async Task GivenActiveTab_WhenACommitIsMadeElsewhere_ThenAFullRefreshIsRequestedWithinTheInterval()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository);
        harness.Poller.SetActive(true);
        await harness.WaitForAsync(kinds => kinds == StatusOnly);
        harness.Requests.Clear();

        repository.Commit("second", new TestFile("b.txt", "2"));

        await harness.WaitForAsync(kinds => kinds == RepositoryChangeKinds.All);
    }

    [Fact]
    public async Task GivenActiveTab_WhenCheckedNowAfterACommitElsewhere_ThenAFullRefreshIsRequestedBeforeTheNextTick()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository, Slow);
        harness.Poller.SetActive(true);
        await harness.WaitForAsync(kinds => kinds == StatusOnly);
        harness.Requests.Clear();
        repository.Commit("second", new TestFile("b.txt", "2"));
        await harness.DrainForAsync(Settle);
        Assert.Empty(harness.Requests);

        harness.Poller.CheckNow();

        await harness.WaitForAsync(kinds => kinds == RepositoryChangeKinds.All);
    }

    [Fact]
    public async Task GivenActiveTab_WhenCheckedNowWithNothingChanged_ThenAStatusRefreshIsRequestedAtOnce()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository, Slow);
        harness.Poller.SetActive(true);
        await harness.WaitForAsync(kinds => kinds == StatusOnly);
        harness.Requests.Clear();

        harness.Poller.CheckNow();

        await harness.WaitForAsync(kinds => kinds == StatusOnly);
        Assert.DoesNotContain(RepositoryChangeKinds.All, harness.Requests);
    }

    [Fact]
    public async Task GivenHiddenTab_WhenCheckedNow_ThenNothingIsRead()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository, Slow);

        harness.Poller.CheckNow();
        await harness.DrainForAsync(Settle);

        Assert.True(harness.Posted.IsEmpty);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task GivenActiveTab_WhenNothingChanges_ThenOnlyStatusRefreshesAreRequested()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository);

        harness.Poller.SetActive(true);

        await harness.WaitForAsync(kinds => kinds == StatusOnly);
        await harness.DrainForAsync(Settle);
        Assert.DoesNotContain(RepositoryChangeKinds.All, harness.Requests);
    }

    [Fact]
    public async Task GivenHiddenTab_WhenTheRepositoryChanges_ThenNothingIsPolledUntilItIsShown()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository);
        harness.Poller.SetActive(true);
        await harness.WaitForAsync(kinds => kinds == StatusOnly);
        harness.Poller.SetActive(false);
        await harness.DrainForAsync(Settle);
        harness.Requests.Clear();

        repository.Commit("second", new TestFile("b.txt", "2"));
        await Task.Delay(Settle, TestContext.Current.CancellationToken);

        Assert.True(harness.Posted.IsEmpty);
        harness.Poller.SetActive(true);
        await harness.WaitForAsync(kinds => kinds == RepositoryChangeKinds.All);
    }

    [Fact]
    public async Task GivenTabThatCannotPoll_WhenTicksArrive_ThenNothingIsRequestedUntilItCan()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository);
        harness.CanPoll = false;

        harness.Poller.SetActive(true);
        await harness.DrainForAsync(Settle);

        Assert.Empty(harness.Requests);
        harness.CanPoll = true;
        await harness.WaitForAsync(kinds => kinds == StatusOnly);
    }

    [Fact]
    public async Task GivenActiveTab_WhenItIsDisposed_ThenNoMoreRequestsAreMade()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository);
        harness.Poller.SetActive(true);
        await harness.WaitForAsync(kinds => kinds == StatusOnly);

        harness.Poller.Dispose();
        await harness.DrainForAsync(Settle);
        harness.Requests.Clear();
        await harness.DrainForAsync(Settle);

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task GivenSlowRefresh_WhenItIsRecorded_ThenTheStatusIntervalGrowsAndTheNextStatusRefreshWaits()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.StartAsync(repository);
        harness.Poller.SetActive(true);
        await harness.WaitForAsync(kinds => kinds == StatusOnly);
        harness.Requests.Clear();

        harness.Poller.RecordRefresh(TimeSpan.FromMilliseconds(200));
        await harness.DrainForAsync(TimeSpan.FromMilliseconds(600));

        Assert.Equal(TimeSpan.FromSeconds(1), harness.Poller.StatusInterval);
        Assert.Empty(harness.Requests);
    }

    private sealed class Harness : IDisposable
    {
        private Harness(RepositoryLocation location, PollOptions options)
        {
            Poller = new TabPoller(location, options, Posted.Enqueue, () => CanPoll, Requests.Add);
            Poller.Start();
        }

        public TabPoller Poller { get; }

        public ConcurrentQueue<Action> Posted { get; } = new();

        public List<RepositoryChangeKinds> Requests { get; } = [];

        public bool CanPoll { get; set; } = true;

        public static async Task<Harness> StartAsync(TestRepository repository, PollOptions? options = null)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);

            return new Harness(location, options ?? Fast);
        }

        /// <summary>Runs posted callbacks on this thread until a request satisfies the predicate.</summary>
        public async Task WaitForAsync(Func<RepositoryChangeKinds, bool> predicate)
        {
            using var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            guard.CancelAfter(TimeSpan.FromSeconds(30));
            while (!Requests.Exists(predicate.Invoke))
            {
                DrainOnce();
                await Task.Delay(10, guard.Token);
            }
        }

        public async Task DrainForAsync(TimeSpan duration)
        {
            var end = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < end)
            {
                DrainOnce();
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        public void Dispose() => Poller.Dispose();

        private void DrainOnce()
        {
            while (Posted.TryDequeue(out var action))
            {
                action();
            }
        }
    }
}
