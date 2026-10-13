using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading;
using Xunit;

namespace Gitunia.Tui.Tests.Loading;

public class CommitDetailsPrefetchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenNeighbours_WhenSelectingACommit_ThenTheyAreReadInTheBackgroundAndSelectingThemIsInstant()
    {
        using var repository = ChainOfCommits(4);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repo, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        var head = IdOf(repository, "HEAD");
        var parent = IdOf(repository, "HEAD~1");
        var grandparent = IdOf(repository, "HEAD~2");

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
        using var repository = ChainOfCommits(3);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repo, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        var head = IdOf(repository, "HEAD");
        var parent = IdOf(repository, "HEAD~1");
        store.Select(head);
        await WaitForPostsAsync(posted, 1);

        store.Select(parent);
        await WaitForPostsAsync(posted, 1);
        store.Select(head, [parent, IdOf(repository, "HEAD~2")]);
        await WaitForPostsAsync(posted, 1);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(3, store.CachedCount);
    }

    [Fact]
    public async Task GivenPrefetchWaitingForTheDebounce_WhenAnotherCommitIsSelected_ThenThePrefetchIsCancelled()
    {
        using var repository = ChainOfCommits(3);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var head = IdOf(repository, "HEAD");
        var parent = IdOf(repository, "HEAD~1");
        var grandparent = IdOf(repository, "HEAD~2");
        var posted = new SemaphoreSlim(0);
        var blocking = false;
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var store = new CommitDetailsStore(
            repo,
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
        using var repository = ChainOfCommits(5);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repo, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1), cache: new CommitDetailsCache(capacity: 2));

        store.Select(IdOf(repository, "HEAD"), [IdOf(repository, "HEAD~1"), IdOf(repository, "HEAD~2"), IdOf(repository, "HEAD~3")]);
        await WaitForPostsAsync(posted, 4);

        Assert.Equal(2, store.CachedCount);
    }

    [Fact]
    public async Task GivenUnreadableNeighbour_WhenPrefetching_ThenTheSelectionStaysLoadedAndTheOtherNeighboursAreRead()
    {
        using var repository = ChainOfCommits(2);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repo, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        Assert.True(ObjectId.TryParse(new string('1', 40), out var missing));

        store.Select(IdOf(repository, "HEAD"), [missing, IdOf(repository, "HEAD~1")]);
        await WaitForPostsAsync(posted, 2);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(2, store.CachedCount);
    }

    [Fact]
    public async Task GivenPauseBetweenMoves_WhenSelecting_ThenTheCommitIsReadAtOnceOnlyAfterThePause()
    {
        using var repository = ChainOfCommits(3);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var debounce = TimeSpan.FromMilliseconds(100);
        var waits = new List<TimeSpan>();
        var posted = new SemaphoreSlim(0);
        var clock = new ManualTimeProvider();
        using var store = new CommitDetailsStore(
            repo,
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

        store.Select(IdOf(repository, "HEAD"));
        await WaitForPostsAsync(posted, 1);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        store.Select(IdOf(repository, "HEAD~1"));
        await WaitForPostsAsync(posted, 1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        store.Select(IdOf(repository, "HEAD~2"));
        await WaitForPostsAsync(posted, 1);

        lock (waits)
        {
            Assert.Equal([TimeSpan.Zero, debounce, TimeSpan.Zero], waits);
        }
    }

    [Fact]
    public async Task GivenUncachedCommit_WhenItLoads_ThenThePrefetchWaitsForTheDebounceToo()
    {
        using var repository = ChainOfCommits(3);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var debounce = TimeSpan.FromMilliseconds(100);
        var waits = new List<TimeSpan>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(
            repo,
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

        store.Select(IdOf(repository, "HEAD"), [IdOf(repository, "HEAD~1")]);
        await WaitForPostsAsync(posted, 2);

        lock (waits)
        {
            Assert.Equal([TimeSpan.Zero, debounce], waits);
        }
    }

    [Fact]
    public async Task GivenResultsQueuedForTheUiThread_WhenAnotherCommitIsSelectedBeforeTheyRun_ThenTheyAreDropped()
    {
        using var repository = ChainOfCommits(3);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var queued = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repo, action => { queued.Enqueue(action); posted.Release(); }, TimeSpan.FromMilliseconds(1));

        store.Select(IdOf(repository, "HEAD"), [IdOf(repository, "HEAD~1")]);
        await WaitForPostsAsync(posted, 2);
        var other = IdOf(repository, "HEAD~2");
        store.Select(other);
        while (queued.TryDequeue(out var action))
        {
            action();
        }

        Assert.Equal(other, store.Current.Commit);
        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenPrefetchedDetailsAlreadyOnTheUiThread_WhenAnotherCommitIsSelected_ThenTheyStayCached()
    {
        using var repository = ChainOfCommits(3);
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var queued = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repo, action => { queued.Enqueue(action); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        store.Select(IdOf(repository, "HEAD"), [IdOf(repository, "HEAD~1")]);
        await WaitForPostsAsync(posted, 2);
        while (queued.TryDequeue(out var action))
        {
            action();
        }

        store.Select(IdOf(repository, "HEAD~2"));

        Assert.Equal(2, store.CachedCount);
    }

    private static TestRepository ChainOfCommits(int count)
    {
        var repository = TestRepository.Create();
        for (var number = 1; number <= count; number++)
        {
            repository.Commit("commit " + number, ("a.txt", number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return repository;
    }

    private static ObjectId IdOf(TestRepository repository, string revision)
    {
        Assert.True(ObjectId.TryParse(repository.Git("rev-parse", revision).Trim(), out var id));
        return id;
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
