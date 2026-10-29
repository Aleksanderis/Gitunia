using System.Runtime.CompilerServices;
using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
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

    [Fact]
    public async Task GivenAMergeCommit_WhenCyclingTheParent_ThenFilesComeFromEachParentAndWrapToTheFirst()
    {
        var reader = new FakeCommitDetailsReader();
        var main = reader.Add("main");
        var side = reader.Add("side");
        var merge = reader.AddMerge("merge", main, side);
        reader.SetFiles(merge, main, "from-main.txt");
        reader.SetFiles(merge, side, "from-side.txt");
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(merge);
        await WaitForSettledAsync(pump, store, merge);

        Assert.Equal(1, store.CycleParent());
        await pump.RunUntilAsync(() => store.Current.Details!.ParentIndex == 1);
        Assert.Equal(["from-side.txt"], store.Current.Details!.Files.Select(file => file.Path));

        Assert.Equal(0, store.CycleParent());
        await pump.RunUntilAsync(() => store.Current.Details!.ParentIndex == 0);
        Assert.Equal(["from-main.txt"], store.Current.Details!.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task GivenAPlainCommit_WhenCyclingTheParent_ThenNothingStarts()
    {
        var reader = new FakeCommitDetailsReader();
        var commit = reader.Add("one", reader.Add("root"));
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));

        Assert.Null(store.CycleParent());
        store.Select(commit);
        await WaitForSettledAsync(pump, store, commit);

        Assert.Null(store.CycleParent());
        Assert.Equal(0, reader.ReadFilesCount);
    }

    [Fact]
    public async Task GivenASwitchedMerge_WhenSelectingAnotherCommitAndBack_ThenTheFirstParentIsShownAgain()
    {
        var reader = new FakeCommitDetailsReader();
        var main = reader.Add("main");
        var side = reader.Add("side");
        var merge = reader.AddMerge("merge", main, side);
        reader.SetFiles(merge, side, "from-side.txt");
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(merge);
        await WaitForSettledAsync(pump, store, merge);
        store.CycleParent();
        await pump.RunUntilAsync(() => store.Current.Details!.ParentIndex == 1);

        store.Select(main);
        await WaitForSettledAsync(pump, store, main);
        store.Select(merge);

        Assert.Equal(0, store.Current.Details!.ParentIndex);
        Assert.Equal(1, store.CycleParent());
    }

    [Fact]
    public async Task GivenARealMergeCommit_WhenCyclingTheParent_ThenFilesAreAgainstTheSecondParent()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", new TestFile("side.txt", "side\n"))
            .Checkout("main")
            .Commit("main work", new TestFile("main.txt", "main\n"))
            .Merge("side");
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", "HEAD").Trim(), out var merge));
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            TestGit.Services.CommitDetails,
            location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(merge);
        await WaitForSettledAsync(pump, store, merge);
        Assert.Equal(["side.txt"], store.Current.Details!.Files.Select(file => file.Path));

        store.CycleParent();
        await pump.RunUntilAsync(() => store.Current.Details!.ParentIndex == 1);

        Assert.Equal(["main.txt"], store.Current.Details!.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task GivenAParentReadInFlight_WhenCyclingAgain_ThenTheFirstReadIsCancelledAndItsLateResultDropped()
    {
        var reader = new FakeCommitDetailsReader();
        var first = reader.Add("first");
        var second = reader.Add("second");
        var third = reader.Add("third");
        var merge = reader.AddMerge("merge", first, second, third);
        reader.SetFiles(merge, second, "from-second.txt");
        reader.SetFiles(merge, third, "from-third.txt");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelledSeen = new StrongBox<bool>();
        var blocking = new BlockingFilesReader(reader, async (parent, token) =>
        {
            if (parent != second)
            {
                return await reader.ReadFilesAsync(FakeCommitDetailsReader.Location, merge, parent, null, token);
            }

            started.TrySetResult();
            await release.Task;
            cancelledSeen.Value = token.IsCancellationRequested;
            return await reader.ReadFilesAsync(
                FakeCommitDetailsReader.Location, merge, parent, null, CancellationToken.None);
        });
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            blocking,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(merge);
        await WaitForSettledAsync(pump, store, merge);
        Assert.Equal(1, store.CycleParent());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.Equal(2, store.CycleParent());
        await pump.RunUntilAsync(() => store.Current.Details!.ParentIndex == 2);
        release.SetResult();
        await pump.RunPostsAsync(1);

        Assert.True(cancelledSeen.Value);
        Assert.Equal(2, store.Current.Details!.ParentIndex);
        Assert.Equal(["from-third.txt"], store.Current.Details.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task GivenAParentReadInFlight_WhenSelectingAnotherCommit_ThenTheStaleResultIsDropped()
    {
        var reader = new FakeCommitDetailsReader();
        var main = reader.Add("main");
        var side = reader.Add("side");
        var merge = reader.AddMerge("merge", main, side);
        reader.SetFiles(merge, side, "from-side.txt");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocking = new BlockingFilesReader(reader, async (parent, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return await reader.ReadFilesAsync(
                FakeCommitDetailsReader.Location, merge, parent, null, CancellationToken.None);
        });
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            blocking,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(merge);
        await WaitForSettledAsync(pump, store, merge);
        store.CycleParent();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        store.Select(main);
        await WaitForSettledAsync(pump, store, main);

        release.SetResult();
        await pump.RunPostsAsync(1);

        Assert.Equal(main, store.Current.Commit);
        Assert.Equal("main", store.Current.Details!.Subject);
        Assert.Equal(0, store.Current.Details.ParentIndex);
    }

    [Fact]
    public async Task GivenAFailedParentRead_WhenCyclingAgain_ThenTheOldFilesStayAndTheSameParentIsRetried()
    {
        var reader = new FakeCommitDetailsReader();
        var main = reader.Add("main");
        var side = reader.Add("side");
        var merge = reader.AddMerge("merge", main, side);
        reader.SetFiles(merge, main, "from-main.txt");
        reader.SetFiles(merge, side, "from-side.txt");
        var failNext = new StrongBox<bool>(true);
        var blocking = new BlockingFilesReader(reader, (parent, token) =>
        {
            if (failNext.Value)
            {
                throw new InvalidOperationException("boom");
            }

            return reader.ReadFilesAsync(FakeCommitDetailsReader.Location, merge, parent, null, token);
        });
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            blocking,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(merge);
        await WaitForSettledAsync(pump, store, merge);
        var before = store.Current;
        Assert.Equal(1, store.CycleParent());
        await pump.RunPostsAsync(1);

        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(before.Details, store.Current.Details);

        failNext.Value = false;
        Assert.Equal(1, store.CycleParent());
        await pump.RunUntilAsync(() => store.Current.Details!.ParentIndex == 1);

        Assert.Equal(["from-side.txt"], store.Current.Details!.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task GivenALoadedCommit_WhenComparingItWithAnother_ThenTheFilesAreTheDiffBetweenTheTwo()
    {
        var reader = new FakeCommitDetailsReader();
        var older = reader.Add("older");
        var newer = reader.Add("newer", older);
        reader.SetFiles(newer, older, "between.txt");
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(older);
        await WaitForSettledAsync(pump, store, older);

        Assert.True(store.Compare(older, newer));
        await pump.RunUntilAsync(() => store.Current.Details!.IsComparison);

        Assert.Equal(older, store.Current.Commit);
        Assert.Equal(["between.txt"], store.Current.Details!.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task GivenACommitThatIsNeitherEnd_WhenComparing_ThenNothingStarts()
    {
        var reader = new FakeCommitDetailsReader();
        var first = reader.Add("first");
        var second = reader.Add("second", first);
        var third = reader.Add("third", second);
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        Assert.False(store.Compare(first, second));
        store.Select(third);
        await WaitForSettledAsync(pump, store, third);

        Assert.False(store.Compare(first, second));
        Assert.Equal(0, reader.ReadFilesCount);
    }

    [Fact]
    public async Task GivenAComparison_WhenClearingIt_ThenTheCommitsOwnFilesAreBack()
    {
        var reader = new FakeCommitDetailsReader();
        var older = reader.Add("older");
        var newer = reader.Add("newer", older);
        reader.SetFiles(newer, older, "between.txt");
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(newer);
        await WaitForSettledAsync(pump, store, newer);
        store.Compare(older, newer);
        await pump.RunUntilAsync(() => store.Current.Details!.IsComparison);

        Assert.True(store.ClearComparison());

        Assert.False(store.Current.Details!.IsComparison);
        Assert.Empty(store.Current.Details.Files);
        Assert.False(store.ClearComparison());
    }

    [Fact]
    public async Task GivenAComparisonStillLoading_WhenSelectingAnotherCommit_ThenTheLateResultIsDropped()
    {
        var reader = new FakeCommitDetailsReader();
        var older = reader.Add("older");
        var newer = reader.Add("newer", older);
        var other = reader.Add("other", newer);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocking = new BlockingFilesReader(reader, async (_, _) =>
        {
            await release.Task;
            return [new CommitFileChange("late.txt", null, FileChangeKind.Modified, null)];
        });
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            blocking,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(newer);
        await WaitForSettledAsync(pump, store, newer);
        store.Compare(older, newer);

        store.Select(other);
        await WaitForSettledAsync(pump, store, other);
        release.SetResult();
        await pump.RunPostsAsync(1);

        Assert.Equal(other, store.Current.Commit);
        Assert.False(store.Current.Details!.IsComparison);
    }

    [Fact]
    public async Task GivenAComparison_WhenCyclingTheParent_ThenNothingStarts()
    {
        var reader = new FakeCommitDetailsReader();
        var main = reader.Add("main");
        var side = reader.Add("side");
        var merge = reader.AddMerge("merge", main, side);
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(merge);
        await WaitForSettledAsync(pump, store, merge);
        store.Compare(main, merge);
        await pump.RunUntilAsync(() => store.Current.Details!.IsComparison);

        Assert.Null(store.CycleParent());
    }

    [Fact]
    public async Task GivenTwoCommitsOnDifferentBranches_WhenComparingThroughGit_ThenTheFilesMatchGitDiffOfTheTwo()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("base.txt", "base\n"), new TestFile("gone.txt", "bye\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", new TestFile("side.txt", "side\n"), new TestFile("base.txt", "side base\n"))
            .Checkout("main")
            .Commit("main work", new TestFile("main.txt", "main\n"));
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", "side").Trim(), out var side));
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", "main").Trim(), out var main));
        var pump = new UiPump(Token);
        using var store = new CommitDetailsStore(
            TestGit.Services.CommitDetails,
            location,
            pump.Post,
            TimeSpan.FromMilliseconds(1));
        store.Select(main);
        await WaitForSettledAsync(pump, store, main);

        store.Compare(side, main);
        await pump.RunUntilAsync(() => store.Current.Details!.IsComparison);

        var expected = testRepository.Git("diff", "--name-only", side.ToString(), main.ToString())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected.Order(), store.Current.Details!.Files.Select(file => file.Path).Order());
        Assert.Contains("main.txt", expected);
        Assert.Contains("side.txt", expected);
    }

    private static async Task<CommitDetailsState> WaitForSettledAsync(UiPump pump, CommitDetailsStore store, ObjectId commit)
    {
        await pump.RunUntilAsync(() => store.Current.Commit == commit && store.Current.Status is CommitDetailsStatus.Loaded or CommitDetailsStatus.Failed);
        return store.Current;
    }

    /// <summary>Delegates reads to a fake, but lets a test decide how each file read behaves (block, fail).</summary>
    private sealed class BlockingFilesReader(
        FakeCommitDetailsReader inner,
        Func<ObjectId, CancellationToken, Task<IReadOnlyList<CommitFileChange>>> files) : ICommitDetailsReader
    {
        public Task<CommitDetails?> ReadAsync(
            RepositoryLocation location,
            ObjectId id,
            ICollection<string>? warnings = null,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(location, id, warnings, cancellationToken);

        public Task<IReadOnlyList<CommitFileChange>> ReadFilesAsync(
            RepositoryLocation location,
            ObjectId id,
            ObjectId? parent,
            ICollection<string>? warnings = null,
            CancellationToken cancellationToken = default) =>
            files(parent!.Value, cancellationToken);
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
