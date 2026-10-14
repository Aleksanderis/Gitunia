using System.Collections.Concurrent;
using System.Globalization;
using Gitunia.Git;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Commits;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Commits;

public class CommitDetailsPrefetchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenNeighbours_WhenSelectingACommit_ThenTheyAreReadInTheBackgroundAndSelectingThemIsInstant()
    {
        using var testRepository = ChainOfCommits(4);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repository, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        var head = IdOf(testRepository, "HEAD");
        var parent = IdOf(testRepository, "HEAD~1");
        var grandparent = IdOf(testRepository, "HEAD~2");

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
        using var testRepository = ChainOfCommits(3);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repository, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        var head = IdOf(testRepository, "HEAD");
        var parent = IdOf(testRepository, "HEAD~1");
        store.Select(head);
        await WaitForPostsAsync(posted, 1);

        store.Select(parent);
        await WaitForPostsAsync(posted, 1);
        store.Select(head, [parent, IdOf(testRepository, "HEAD~2")]);
        await WaitForPostsAsync(posted, 1);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(3, store.CachedCount);
    }

    [Fact]
    public async Task GivenPrefetchWaitingForTheDebounce_WhenAnotherCommitIsSelected_ThenThePrefetchIsCancelled()
    {
        using var testRepository = ChainOfCommits(3);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var head = IdOf(testRepository, "HEAD");
        var parent = IdOf(testRepository, "HEAD~1");
        var grandparent = IdOf(testRepository, "HEAD~2");
        var posted = new SemaphoreSlim(0);
        var blocking = false;
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var store = new CommitDetailsStore(
            repository,
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
        using var testRepository = ChainOfCommits(5);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repository, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1), cache: new CommitDetailsCache(capacity: 2));

        store.Select(IdOf(testRepository, "HEAD"), [IdOf(testRepository, "HEAD~1"), IdOf(testRepository, "HEAD~2"), IdOf(testRepository, "HEAD~3")]);
        await WaitForPostsAsync(posted, 4);

        Assert.Equal(2, store.CachedCount);
    }

    [Fact]
    public async Task GivenUnreadableNeighbour_WhenPrefetching_ThenTheSelectionStaysLoadedAndTheOtherNeighboursAreRead()
    {
        using var testRepository = ChainOfCommits(2);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repository, action => { action(); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        Assert.True(ObjectId.TryParse(new string('1', 40), out var missing));

        store.Select(IdOf(testRepository, "HEAD"), [missing, IdOf(testRepository, "HEAD~1")]);
        await WaitForPostsAsync(posted, 2);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(2, store.CachedCount);
    }

    [Fact]
    public async Task GivenPauseBetweenMoves_WhenSelecting_ThenTheCommitIsReadAtOnceOnlyAfterThePause()
    {
        using var testRepository = ChainOfCommits(3);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var debounce = TimeSpan.FromMilliseconds(100);
        var waits = new List<TimeSpan>();
        var posted = new SemaphoreSlim(0);
        var clock = new ManualTimeProvider();
        using var store = new CommitDetailsStore(
            repository,
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

        store.Select(IdOf(testRepository, "HEAD"));
        await WaitForPostsAsync(posted, 1);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        store.Select(IdOf(testRepository, "HEAD~1"));
        await WaitForPostsAsync(posted, 1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        store.Select(IdOf(testRepository, "HEAD~2"));
        await WaitForPostsAsync(posted, 1);

        lock (waits)
        {
            Assert.Equal([TimeSpan.Zero, debounce, TimeSpan.Zero], waits);
        }
    }

    [Fact]
    public async Task GivenUncachedCommit_WhenItLoads_ThenThePrefetchWaitsForTheDebounceToo()
    {
        using var testRepository = ChainOfCommits(3);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var debounce = TimeSpan.FromMilliseconds(100);
        var waits = new List<TimeSpan>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(
            repository,
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

        store.Select(IdOf(testRepository, "HEAD"), [IdOf(testRepository, "HEAD~1")]);
        await WaitForPostsAsync(posted, 2);

        lock (waits)
        {
            Assert.Equal([TimeSpan.Zero, debounce], waits);
        }
    }

    [Fact]
    public async Task GivenResultsQueuedForTheUiThread_WhenAnotherCommitIsSelectedBeforeTheyRun_ThenTheyAreDropped()
    {
        using var testRepository = ChainOfCommits(3);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var queued = new ConcurrentQueue<Action>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repository, action => { queued.Enqueue(action); posted.Release(); }, TimeSpan.FromMilliseconds(1));

        store.Select(IdOf(testRepository, "HEAD"), [IdOf(testRepository, "HEAD~1")]);
        await WaitForPostsAsync(posted, 2);
        var other = IdOf(testRepository, "HEAD~2");
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
        using var testRepository = ChainOfCommits(3);
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, Token);
        var queued = new ConcurrentQueue<Action>();
        var posted = new SemaphoreSlim(0);
        using var store = new CommitDetailsStore(repository, action => { queued.Enqueue(action); posted.Release(); }, TimeSpan.FromMilliseconds(1));
        store.Select(IdOf(testRepository, "HEAD"), [IdOf(testRepository, "HEAD~1")]);
        await WaitForPostsAsync(posted, 2);
        while (queued.TryDequeue(out var action))
        {
            action();
        }

        store.Select(IdOf(testRepository, "HEAD~2"));

        Assert.Equal(2, store.CachedCount);
    }

    private static TestRepository ChainOfCommits(int count)
    {
        var testRepository = TestRepository.Create();
        for (var number = 1; number <= count; number++)
        {
            testRepository.Commit($"commit {number}", new TestFile("a.txt", number.ToString(CultureInfo.InvariantCulture)));
        }

        return testRepository;
    }

    private static ObjectId IdOf(TestRepository testRepository, string revision)
    {
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", revision).Trim(), out var id));
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
