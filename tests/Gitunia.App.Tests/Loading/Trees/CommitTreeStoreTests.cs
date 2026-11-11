using Gitunia.App.Loading;
using System.Runtime.CompilerServices;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Trees;
using Gitunia.Git;
using Xunit;

namespace Gitunia.App.Tests.Loading.Trees;

public class CommitTreeStoreTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenASelection_WhenSelecting_ThenLoadingShowsAtOnceAndTheTreeArrivesLater()
    {
        var reader = new FakeCommitTreeReader();
        var commit = FakeObjectIds.Of(1);
        reader.Add(commit, "a.txt", "dir/b.txt");
        var pump = new UiPump(Token);
        using var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump, Debounce);

        store.Select(commit);

        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        Assert.Null(store.Current.Model);
        var settled = await WaitForSettledAsync(pump, store, commit);
        Assert.Equal(CommitDetailsStatus.Loaded, settled.Status);
        Assert.Equal(2, settled.Model!.FileCount);
    }

    [Fact]
    public async Task GivenRapidSelectionChanges_WhenSelecting_ThenOnlyTheLastTreeIsRead()
    {
        var reader = new FakeCommitTreeReader();
        var first = FakeObjectIds.Of(1);
        var second = FakeObjectIds.Of(2);
        var last = FakeObjectIds.Of(3);
        reader.Add(first, "1");
        reader.Add(second, "2");
        reader.Add(last, "3");
        var pump = new UiPump(Token);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var store = new CommitTreeStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump,
            Debounce,
            new CommitTreeStoreOptions(new DelegateDelayer(async (_, token) => await gate.Task.WaitAsync(token))));

        store.Select(first);
        store.Select(second);
        store.Select(last);
        gate.SetResult();
        await WaitForSettledAsync(pump, store, last);

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(last, store.Current.Commit);
    }

    [Fact]
    public async Task GivenATreeSeenBefore_WhenSelectingItAgain_ThenItIsLoadedAtOnceWithoutReading()
    {
        var reader = new FakeCommitTreeReader();
        var first = FakeObjectIds.Of(1);
        var second = FakeObjectIds.Of(2);
        reader.Add(first, "1");
        reader.Add(second, "2");
        var pump = new UiPump(Token);
        using var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump, Debounce);
        store.Select(first);
        await WaitForSettledAsync(pump, store, first);
        store.Select(second);
        await WaitForSettledAsync(pump, store, second);

        store.Select(first);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(2, reader.ReadCount);
    }

    [Fact]
    public async Task GivenMoreTreesThanTheCacheHolds_WhenGoingBack_ThenTheOldestIsReadAgain()
    {
        var reader = new FakeCommitTreeReader();
        var pump = new UiPump(Token);
        using var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump, Debounce);
        var commits = Enumerable.Range(1, CommitTreeStore.CacheCapacity + 1).Select(FakeObjectIds.Of).ToArray();
        foreach (var commit in commits)
        {
            reader.Add(commit, "file");
            store.Select(commit);
            await WaitForSettledAsync(pump, store, commit);
        }

        store.Select(commits[0]);

        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        await WaitForSettledAsync(pump, store, commits[0]);
        Assert.Equal(commits.Length + 1, reader.ReadCount);
    }

    [Fact]
    public async Task GivenAnUnknownCommit_WhenSelecting_ThenTheStateFailsWithAMessage()
    {
        var reader = new FakeCommitTreeReader();
        var pump = new UiPump(Token);
        using var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump, Debounce);
        var missing = FakeCommitDetailsReader.MissingId();

        store.Select(missing);
        var settled = await WaitForSettledAsync(pump, store, missing);

        Assert.Equal(CommitDetailsStatus.Failed, settled.Status);
        Assert.NotEmpty(settled.Error);
    }

    [Fact]
    public async Task GivenAFailedLoadThatCanNowSucceed_WhenRetrying_ThenTheTreeLoads()
    {
        var reader = new FakeCommitTreeReader();
        var pump = new UiPump(Token);
        using var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump, Debounce);
        var commit = FakeObjectIds.Of(1);
        store.Select(commit);
        await WaitForSettledAsync(pump, store, commit);
        reader.Add(commit, "a.txt");

        var started = store.Retry();
        var settled = await WaitForSettledAsync(pump, store, commit);

        Assert.True(started);
        Assert.Equal(CommitDetailsStatus.Loaded, settled.Status);
    }

    [Fact]
    public async Task GivenALoadedTree_WhenRetrying_ThenNothingHappens()
    {
        var reader = new FakeCommitTreeReader();
        var commit = FakeObjectIds.Of(1);
        reader.Add(commit, "a.txt");
        var pump = new UiPump(Token);
        using var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump, Debounce);
        store.Select(commit);
        await WaitForSettledAsync(pump, store, commit);

        Assert.False(store.Retry());
        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
    }

    [Fact]
    public async Task GivenADisposedStore_WhenALoadWasPending_ThenNothingIsPosted()
    {
        var reader = new FakeCommitTreeReader();
        var commit = FakeObjectIds.Of(1);
        reader.Add(commit, "a.txt");
        var posted = new StrongBox<int>();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new CommitTreeStore(
            reader,
            FakeCommitDetailsReader.Location,
            new DelegateUi(action =>
            {
                Interlocked.Increment(ref posted.Value);
                action();
            }),
            TimeSpan.FromMilliseconds(200),
            new CommitTreeStoreOptions(new DelegateDelayer(async (_, token) =>
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
            })));
        store.Select(commit);
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        store.Dispose();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.Equal(0, Volatile.Read(ref posted.Value));
        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task GivenTreesOf100000Files_WhenSelectingSeveral_ThenTheOldestIsEvictedForTheFileBudget()
    {
        var reader = new FakeCommitTreeReader();
        var paths = Enumerable.Range(0, 100_000).Select(index => $"dir{index % 100}/file{index}.txt").ToArray();
        var commits = new[] { FakeObjectIds.Of(1), FakeObjectIds.Of(2), FakeObjectIds.Of(3) };
        foreach (var commit in commits)
        {
            reader.Add(commit, paths);
        }

        var pump = new UiPump(Token);
        using var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump, Debounce);

        foreach (var commit in commits)
        {
            store.Select(commit);
            await WaitForSettledAsync(pump, store, commit);
        }

        Assert.Equal(3, reader.ReadCount);
        store.Select(commits[1]);
        Assert.Equal(3, reader.ReadCount);
        store.Select(commits[0]);
        await WaitForSettledAsync(pump, store, commits[0]);
        Assert.Equal(4, reader.ReadCount);
    }

    [Fact]
    public async Task GivenOneTreeAboveTheFileBudget_WhenSelectingItAgain_ThenItIsServedFromTheCache()
    {
        var reader = new FakeCommitTreeReader();
        var commit = FakeObjectIds.Of(1);
        var other = FakeObjectIds.Of(2);
        reader.Add(
            commit,
            [.. Enumerable.Range(0, CommitTreeStore.CacheFileBudget + 1).Select(index => $"f{index}")]);
        var pump = new UiPump(Token);
        using var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump, Debounce);
        store.Select(commit);
        await WaitForSettledAsync(pump, store, commit);

        store.Select(other);
        await WaitForSettledAsync(pump, store, other);

        store.Select(commit);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(2, reader.ReadCount);
    }

    private static async Task<CommitTreeState> WaitForSettledAsync(UiPump pump, CommitTreeStore store, ObjectId commit)
    {
        await pump.RunUntilAsync(
            () => store.Current.Commit == commit
                && store.Current.Status is CommitDetailsStatus.Loaded or CommitDetailsStatus.Failed);

        return store.Current;
    }
}
