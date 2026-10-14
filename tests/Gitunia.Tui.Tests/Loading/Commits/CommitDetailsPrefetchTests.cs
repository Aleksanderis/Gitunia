using System.Collections.Concurrent;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Commits;

public class CommitDetailsPrefetchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenNeighbours_WhenSelectingACommit_ThenTheyAreReadInTheBackgroundAndSelectingThemIsInstant()
    {
        var reader = FakeCommitDetailsReader.Chain(4);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        var head = reader.Commits[0];
        var parent = reader.Commits[1];
        var grandparent = reader.Commits[2];

        store.Select(head, [parent, grandparent]);
        await WaitForPostsAsync(posted, 3);

        Assert.Equal(3, store.CachedCount);
        store.Select(parent);
        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal("commit 3", store.Current.Details!.Subject);
    }

    [Fact]
    public async Task GivenCachedCommit_WhenSelectingItWithNeighbours_ThenTheNeighboursAreReadAfterTheDebounce()
    {
        var reader = FakeCommitDetailsReader.Chain(3);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        var head = reader.Commits[0];
        var parent = reader.Commits[1];
        store.Select(head);
        await WaitForPostsAsync(posted, 1);

        store.Select(parent);
        await WaitForPostsAsync(posted, 1);
        store.Select(head, [parent, reader.Commits[2]]);
        await WaitForPostsAsync(posted, 1);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(3, store.CachedCount);
    }

    [Fact]
    public async Task GivenPrefetchWaitingForTheDebounce_WhenAnotherCommitIsSelected_ThenThePrefetchIsCancelled()
    {
        var reader = FakeCommitDetailsReader.Chain(3);
        var head = reader.Commits[0];
        var parent = reader.Commits[1];
        var grandparent = reader.Commits[2];
        var posted = new SemaphoreSlim(0);
        var blocking = false;
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var store = new CommitDetailsStore(
            reader, FakeCommitDetailsReader.Location,
            action => { action(); posted.Release(); },
            TimeSpan.FromMilliseconds(200),
            async (_, token) =>
            {
                if (!Volatile.Read(ref blocking))
                {
                    return;
                }

                waiting.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                finally
                {
                    cancelled.TrySetResult();
                }
            });
        store.Select(head);
        await WaitForPostsAsync(posted, 1);
        store.Select(parent);
        await WaitForPostsAsync(posted, 1);
        Volatile.Write(ref blocking, true);

        store.Select(head, [grandparent]);
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        store.Select(parent);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.Equal(2, store.CachedCount);
    }

    [Fact]
    public async Task GivenSmallCache_WhenPrefetchingMoreThanItHolds_ThenTheCacheStaysWithinItsBound()
    {
        var reader = FakeCommitDetailsReader.Chain(5);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1), cache: new CommitDetailsCache(capacity: 2));

        store.Select(reader.Commits[0], [reader.Commits[1], reader.Commits[2], reader.Commits[3]]);
        await WaitForPostsAsync(posted, 4);

        Assert.Equal(2, store.CachedCount);
    }

    [Fact]
    public async Task GivenUnreadableNeighbour_WhenPrefetching_ThenTheSelectionStaysLoadedAndTheOtherNeighboursAreRead()
    {
        var reader = FakeCommitDetailsReader.Chain(2);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        var missing = FakeCommitDetailsReader.MissingId();

        store.Select(reader.Commits[0], [missing, reader.Commits[1]]);
        await WaitForPostsAsync(posted, 2);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(2, store.CachedCount);
    }

    [Fact]
    public async Task GivenPauseBetweenMoves_WhenSelecting_ThenTheCommitIsReadAtOnceOnlyAfterThePause()
    {
        var reader = FakeCommitDetailsReader.Chain(3);
        var debounce = TimeSpan.FromMilliseconds(100);
        var waits = new List<TimeSpan>();
        var posted = new SemaphoreSlim(0);
        var clock = new ManualTimeProvider();
        using var store = new CommitDetailsStore(
            reader, FakeCommitDetailsReader.Location,
            action => { action(); posted.Release(); },
            debounce,
            (wait, _) =>
            {
                lock (waits)
                {
                    waits.Add(wait);
                }

                return Task.CompletedTask;
            },
            timeProvider: clock);

        store.Select(reader.Commits[0]);
        await WaitForPostsAsync(posted, 1);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        store.Select(reader.Commits[1]);
        await WaitForPostsAsync(posted, 1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        store.Select(reader.Commits[2]);
        await WaitForPostsAsync(posted, 1);

        lock (waits)
        {
            Assert.Equal([TimeSpan.Zero, debounce, TimeSpan.Zero], waits);
        }
    }

    [Fact]
    public async Task GivenUncachedCommit_WhenItLoads_ThenThePrefetchWaitsForTheDebounceToo()
    {
        var reader = FakeCommitDetailsReader.Chain(3);
        var debounce = TimeSpan.FromMilliseconds(100);
        var waits = new List<TimeSpan>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(
            reader, FakeCommitDetailsReader.Location,
            action => { action(); posted.Release(); },
            debounce,
            (wait, _) =>
            {
                lock (waits)
                {
                    waits.Add(wait);
                }

                return Task.CompletedTask;
            });

        store.Select(reader.Commits[0], [reader.Commits[1]]);
        await WaitForPostsAsync(posted, 2);

        lock (waits)
        {
            Assert.Equal([TimeSpan.Zero, debounce], waits);
        }
    }

    [Fact]
    public async Task GivenResultsQueuedForTheUiThread_WhenAnotherCommitIsSelectedBeforeTheyRun_ThenTheyAreDropped()
    {
        var reader = FakeCommitDetailsReader.Chain(3);
        var queued = new ConcurrentQueue<Action>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, action => { queued.Enqueue(action); posted.Release(); }, TimeSpan.FromMilliseconds(1));

        store.Select(reader.Commits[0], [reader.Commits[1]]);
        await WaitForPostsAsync(posted, 2);
        var other = reader.Commits[2];
        // Take the results queued so far before selecting: the load of the new selection posts its own result, and running that one would load it.
        var stale = new List<Action>();
        while (queued.TryDequeue(out var action))
        {
            stale.Add(action);
        }

        store.Select(other);
        foreach (var staleAction in stale)
        {
            staleAction();
        }

        Assert.Equal(other, store.Current.Commit);
        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenPrefetchedDetailsAlreadyOnTheUiThread_WhenAnotherCommitIsSelected_ThenTheyStayCached()
    {
        var reader = FakeCommitDetailsReader.Chain(3);
        var queued = new ConcurrentQueue<Action>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, action => { queued.Enqueue(action); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        store.Select(reader.Commits[0], [reader.Commits[1]]);
        await WaitForPostsAsync(posted, 2);
        while (queued.TryDequeue(out var action))
        {
            action();
        }

        store.Select(reader.Commits[2]);

        Assert.Equal(2, store.CachedCount);
    }

    /// <summary>Waits for results posted to the UI (the test posts them inline from the loader thread and counts them).</summary>
    private static async Task WaitForPostsAsync(SemaphoreSlim posted, int count)
    {
        for (var index = 0; index < count; index++)
        {
            await posted.WaitAsync(TimeSpan.FromSeconds(30), Token);
        }
    }
}
