using System.Runtime.CompilerServices;
using Gitunia.Git;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Tests.App;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Commits;

public class CommitDetailsStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenSelection_WhenSelecting_ThenLoadingShowsAtOnceAndDetailsArriveLater()
    {
        var reader = new FakeCommitDetailsReader();
        var head = reader.Add("second", reader.Add("root"));
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, pump.Post, TimeSpan.FromMilliseconds(1));

        store.Select(head);

        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        Assert.Equal(head, store.Current.Commit);
        var settled = await WaitForSettledAsync(pump, store, head);
        Assert.Equal(CommitDetailsStatus.Loaded, settled.Status);
        Assert.Equal("second", settled.Details!.Subject);
    }

    [Fact]
    public async Task GivenRapidSelectionChanges_WhenSelecting_ThenOnlyTheLastCommitIsLoaded()
    {
        var reader = new FakeCommitDetailsReader();
        var first = reader.Add("one");
        var second = reader.Add("two", first);
        var last = reader.Add("three", second);
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, pump.Post, TimeSpan.FromMilliseconds(300));
        var recorder = new ChangeRecorder(store);

        store.Select(first);
        store.Select(second);
        store.Select(last);
        var settled = await WaitForSettledAsync(pump, store, last);

        Assert.Equal("three", settled.Details!.Subject);
        Assert.DoesNotContain(recorder.Seen, entry => entry.Status == CommitDetailsStatus.Loaded && entry.Commit != last);
        Assert.Equal(last, store.Current.Commit);
    }

    [Fact]
    public async Task GivenCommitSeenBefore_WhenSelectingItAgain_ThenItIsLoadedWithoutWaiting()
    {
        var reader = new FakeCommitDetailsReader();
        var first = reader.Add("one");
        var second = reader.Add("two", first);
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, pump.Post, TimeSpan.FromMilliseconds(1));
        store.Select(first);
        await WaitForSettledAsync(pump, store, first);
        store.Select(second);
        await WaitForSettledAsync(pump, store, second);

        store.Select(first);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal("one", store.Current.Details!.Subject);
    }

    [Fact]
    public async Task GivenUnknownCommit_WhenSelecting_ThenStateFailsWithAMessage()
    {
        var reader = new FakeCommitDetailsReader();
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, pump.Post, TimeSpan.FromMilliseconds(1));
        var missing = FakeCommitDetailsReader.MissingId();

        store.Select(missing);
        var settled = await WaitForSettledAsync(pump, store, missing);

        Assert.Equal(CommitDetailsStatus.Failed, settled.Status);
        Assert.NotEmpty(settled.Error);
    }

    [Fact]
    public async Task GivenFailedLoad_WhenRetrying_ThenItLoadsAgainAndSettles()
    {
        var reader = new FakeCommitDetailsReader();
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, pump.Post, TimeSpan.FromMilliseconds(1));
        var missing = FakeCommitDetailsReader.MissingId();
        store.Select(missing);
        await WaitForSettledAsync(pump, store, missing);

        var started = store.Retry();

        Assert.True(started);
        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        Assert.Equal(CommitDetailsStatus.Failed, (await WaitForSettledAsync(pump, store, missing)).Status);
    }

    [Fact]
    public async Task GivenLoadedCommit_WhenRetrying_ThenNothingHappens()
    {
        var reader = new FakeCommitDetailsReader();
        var head = reader.Add("one");
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(reader, FakeCommitDetailsReader.Location, pump.Post, TimeSpan.FromMilliseconds(1));
        store.Select(head);
        await WaitForSettledAsync(pump, store, head);

        Assert.False(store.Retry());
        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
    }

    [Fact]
    public async Task GivenDisposedStore_WhenLoadWasPending_ThenNothingIsPosted()
    {
        var reader = new FakeCommitDetailsReader();
        var head = reader.Add("one");
        var posted = new StrongBox<int>();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new CommitDetailsStore(
            reader, FakeCommitDetailsReader.Location,
            action => { Interlocked.Increment(ref posted.Value); action(); },
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
        store.Select(head);
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        store.Dispose();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.Equal(0, Volatile.Read(ref posted.Value));
    }

    private static async Task<CommitDetailsState> WaitForSettledAsync(UiPump pump, CommitDetailsStore store, ObjectId commit)
    {
        await pump.RunUntilAsync(() => store.Current.Commit == commit && store.Current.Status is CommitDetailsStatus.Loaded or CommitDetailsStatus.Failed);
        return store.Current;
    }

    private sealed class ChangeRecorder
    {
        private readonly CommitDetailsStore _store;

        public ChangeRecorder(CommitDetailsStore store)
        {
            _store = store;
            store.Changed += OnChanged;
        }

        public List<CommitDetailsState> Seen { get; } = [];

        private void OnChanged() => Seen.Add(_store.Current);
    }
}
