using Gitunia.Git;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Commits;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Commits;

public class CommitDetailsStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ObjectId IdOf(TestRepository testRepository, string revision)
    {
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", revision).Trim(), out var id));
        return id;
    }

    /// <summary>Waits until the store reports the commit as loaded or failed; the results are posted inline from the loader thread.</summary>
    private static async Task<CommitDetailsState> WaitForSettledAsync(CommitDetailsStore store, ObjectId commit)
    {
        var settled = new TaskCompletionSource<CommitDetailsState>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged()
        {
            var current = store.Current;
            if (current.Commit == commit && current.Status is CommitDetailsStatus.Loaded or CommitDetailsStatus.Failed)
            {
                settled.TrySetResult(current);
            }
        }

        store.Changed += OnChanged;
        OnChanged();
        return await settled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
    }

    [Fact]
    public async Task GivenSelection_WhenSelecting_ThenLoadingShowsAtOnceAndDetailsArriveLater()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "1")).Commit("second", new TestFile("a.txt", "2"));
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, Token);
        using var store = new CommitDetailsStore(repository, action => action(), TimeSpan.FromMilliseconds(1));
        var head = IdOf(testRepository, "HEAD");

        store.Select(head);

        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        Assert.Equal(head, store.Current.Commit);
        var settled = await WaitForSettledAsync(store, head);
        Assert.Equal(CommitDetailsStatus.Loaded, settled.Status);
        Assert.Equal("second", settled.Details!.Subject);
    }

    [Fact]
    public async Task GivenRapidSelectionChanges_WhenSelecting_ThenOnlyTheLastCommitIsLoaded()
    {
        using var testRepository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"))
            .Commit("three", new TestFile("a.txt", "3"));
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, Token);
        using var store = new CommitDetailsStore(repository, action => action(), TimeSpan.FromMilliseconds(300));
        var seen = new List<(ObjectId? Commit, CommitDetailsStatus Status)>();
        store.Changed += () => seen.Add((store.Current.Commit, store.Current.Status));
        var first = IdOf(testRepository, "HEAD~2");
        var second = IdOf(testRepository, "HEAD~1");
        var last = IdOf(testRepository, "HEAD");

        store.Select(first);
        store.Select(second);
        store.Select(last);
        var settled = await WaitForSettledAsync(store, last);

        Assert.Equal("three", settled.Details!.Subject);
        Assert.DoesNotContain(seen, entry => entry.Status == CommitDetailsStatus.Loaded && entry.Commit != last);
        Assert.Equal(last, store.Current.Commit);
    }

    [Fact]
    public async Task GivenCommitSeenBefore_WhenSelectingItAgain_ThenItIsLoadedWithoutWaiting()
    {
        using var testRepository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1")).Commit("two", new TestFile("a.txt", "2"));
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, Token);
        using var store = new CommitDetailsStore(repository, action => action(), TimeSpan.FromMilliseconds(1));
        var first = IdOf(testRepository, "HEAD~1");
        var second = IdOf(testRepository, "HEAD");
        store.Select(first);
        await WaitForSettledAsync(store, first);
        store.Select(second);
        await WaitForSettledAsync(store, second);

        store.Select(first);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal("one", store.Current.Details!.Subject);
    }

    [Fact]
    public async Task GivenUnknownCommit_WhenSelecting_ThenStateFailsWithAMessage()
    {
        using var testRepository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, Token);
        using var store = new CommitDetailsStore(repository, action => action(), TimeSpan.FromMilliseconds(1));
        Assert.True(ObjectId.TryParse(new string('1', 40), out var missing));

        store.Select(missing);
        var settled = await WaitForSettledAsync(store, missing);

        Assert.Equal(CommitDetailsStatus.Failed, settled.Status);
        Assert.NotEmpty(settled.Error);
    }

    [Fact]
    public async Task GivenFailedLoad_WhenRetrying_ThenItLoadsAgainAndSettles()
    {
        using var testRepository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, Token);
        using var store = new CommitDetailsStore(repository, action => action(), TimeSpan.FromMilliseconds(1));
        Assert.True(ObjectId.TryParse(new string('1', 40), out var missing));
        store.Select(missing);
        await WaitForSettledAsync(store, missing);

        var started = store.Retry();

        Assert.True(started);
        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        Assert.Equal(CommitDetailsStatus.Failed, (await WaitForSettledAsync(store, missing)).Status);
    }

    [Fact]
    public async Task GivenLoadedCommit_WhenRetrying_ThenNothingHappens()
    {
        using var testRepository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, Token);
        using var store = new CommitDetailsStore(repository, action => action(), TimeSpan.FromMilliseconds(1));
        var head = IdOf(testRepository, "HEAD");
        store.Select(head);
        await WaitForSettledAsync(store, head);

        Assert.False(store.Retry());
        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
    }

    [Fact]
    public async Task GivenDisposedStore_WhenLoadWasPending_ThenNothingIsPosted()
    {
        using var testRepository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, Token);
        var posted = 0;
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new CommitDetailsStore(
            repository,
            action => { Interlocked.Increment(ref posted); action(); },
            TimeSpan.FromMilliseconds(200),
            async (_, token) =>
            {
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
        store.Select(IdOf(testRepository, "HEAD"));
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        store.Dispose();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.Equal(0, Volatile.Read(ref posted));
    }
}
