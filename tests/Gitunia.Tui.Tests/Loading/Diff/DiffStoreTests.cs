using Gitunia.Core.Diff;
using Gitunia.Git;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Diff;

public class DiffStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAModifiedFile_WhenLoading_ThenRowsShowTheChange()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\ntwo\n")).Commit("edit", new TestFile("a.txt", "one\nTWO\n"));
        using var store = await CreateStoreAsync(testRepository);

        var document = await LoadAsync(store, testRepository, "HEAD", "HEAD~1");

        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Removed, Text: "two" });
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "TWO" });
    }

    [Fact]
    public async Task GivenARootCommit_WhenLoading_ThenEverythingIsAdded()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\n"));
        using var store = await CreateStoreAsync(testRepository);

        var document = await LoadAsync(store, testRepository, "HEAD", null);

        Assert.Equal("A a.txt", document.Rows[document.FileRows[0]].Text);
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "one" });
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenLoading_ThenItIsComparedWithTheFirstParent()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", new TestFile("side.txt", "side\n"))
            .Checkout("main")
            .Commit("main work", new TestFile("main.txt", "main\n"))
            .Merge("side");
        using var store = await CreateStoreAsync(testRepository);

        var document = await LoadAsync(store, testRepository, "HEAD", "HEAD^1");

        var paths = document.FilePaths.Select(names => names[0]).ToArray();
        Assert.Equal(["side.txt"], paths);
    }

    [Fact]
    public async Task GivenARenameAndABinaryFile_WhenLoading_ThenBothAreDescribed()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("old.txt", "line one\nline two\nline three\nline four\n"), new TestFile("img.bin", "a\0b"));
        testRepository.Git("mv", "old.txt", "new.txt");
        testRepository.WriteFile("img.bin", "a\0c\0d");
        testRepository.Git("add", "-A");
        testRepository.Git("commit", "-q", "-m", "rename and change binary");
        using var store = await CreateStoreAsync(testRepository);

        var document = await LoadAsync(store, testRepository, "HEAD", "HEAD~1");

        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.FileHeader, Text: "R100 old.txt -> new.txt" });
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Note, Text: "Binary file, content not shown" });
    }

    [Fact]
    public async Task GivenAFiftyThousandLineDiff_WhenLoading_ThenAllRowsArriveAndAnchorsAreSorted()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("big.txt", "start\n"));
        testRepository.Commit("big", new TestFile("big.txt", string.Concat(Enumerable.Range(0, 50_000).Select(index => $"line {index}\n"))));
        using var store = await CreateStoreAsync(testRepository);

        var document = await LoadAsync(store, testRepository, "HEAD", "HEAD~1");

        Assert.True(document.Rows.Count > 50_000);
        Assert.Single(document.HunkRows);
        Assert.Equal(document.HunkRows.Order(), document.HunkRows);
    }

    [Fact]
    public async Task GivenARowLimit_WhenTheDiffIsLonger_ThenItIsCutShortWithANote()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("big.txt", 2000), FakeDiffReader.FileAdding("other.txt", 1));
        using var store = CreateStore(reader, rowLimit: 100);

        var document = await LoadFakeAsync(store, commit);

        Assert.Contains("cut short", document.Rows[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheSameCommit_WhenLoadingAgain_ThenNothingIsStarted()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        using var store = CreateStore(reader);
        await LoadFakeAsync(store, commit);

        Assert.False(store.Load(commit, null));
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task GivenALoadedDiff_WhenClearing_ThenTheStoreIsEmptyAgain()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        using var store = CreateStore(reader);
        await LoadFakeAsync(store, commit);

        store.Clear();

        Assert.Equal(DiffStatus.None, store.Status);
        Assert.Null(store.Commit);
        Assert.Empty(store.Document.Rows);
    }

    [Fact]
    public async Task GivenACommitGitDoesNotKnow_WhenLoading_ThenTheLoadFailsWithAMessage()
    {
        using var store = CreateStore(new FakeDiffReader());

        await LoadFakeAsync(store, FakeDiffReader.MissingId());

        Assert.Equal(DiffStatus.Failed, store.Status);
        Assert.Contains("Could not load the diff", store.Document.Rows[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNeighbours_WhenTheDiffIsShown_ThenTheyArePrefetchedAndShownAtOnce()
    {
        var reader = new FakeDiffReader();
        var first = reader.Add(null, FakeDiffReader.FileAdding("a.txt", ["one"]));
        var second = reader.Add(first, FakeDiffReader.FileAdding("a.txt", ["two"]));
        var third = reader.Add(second, FakeDiffReader.FileAdding("a.txt", ["three"]));
        using var store = CreateStore(reader, delay: (_, _) => Task.CompletedTask);
        var changed = 0;
        store.Changed += () => changed++;
        store.Load(third, second, [second, first]);

        await WaitUntilAsync(() => store.CachedCount == 3);
        var before = changed;
        var started = store.Load(second, first);

        Assert.True(started);
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Equal(before + 1, changed);
        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "two" });
    }

    [Fact]
    public async Task GivenAPrefetchWaiting_WhenAnotherCommitIsSelected_ThenTheStaleNeighboursAreNotCached()
    {
        var reader = new FakeDiffReader();
        var first = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        var second = reader.Add(first, FakeDiffReader.FileAdding("a.txt", 1));
        var third = reader.Add(second, FakeDiffReader.FileAdding("a.txt", 1));
        var gate = new TaskCompletionSource();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var store = CreateStore(reader, delay: async (_, token) =>
        {
            try
            {
                await gate.Task.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        });
        store.Load(third, second, [second]);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded);

        store.Load(first, null);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded && store.Commit == first);

        Assert.Equal(2, store.CachedCount);
        store.Clear();
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenANeighbourOverThePrefetchRowLimit_WhenPrefetching_ThenItIsSkippedAndTheNextOneIsRead()
    {
        var reader = new FakeDiffReader();
        var root = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        var big = reader.Add(root, FakeDiffReader.FileAdding("big.txt", DiffStore.PrefetchRowLimit + 100));
        var small = reader.Add(big, FakeDiffReader.FileAdding("a.txt", 1));
        var tip = reader.Add(small, FakeDiffReader.FileAdding("a.txt", 1));
        using var store = CreateStore(reader, delay: (_, _) => Task.CompletedTask);

        store.Load(tip, small, [big, small]);
        await WaitUntilAsync(() => store.CachedCount == 2);
        await LoadedAsync(store, tip);

        Assert.Equal(2, store.CachedCount);
        Assert.True(store.Load(small, big));
        Assert.Equal(DiffStatus.Loaded, store.Status);
    }

    [Fact]
    public async Task GivenACutDiff_WhenItLoads_ThenItIsNotCached()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("big.txt", 2000));
        using var store = CreateStore(reader, rowLimit: 100);

        await LoadFakeAsync(store, commit);

        Assert.True(store.IsCut);
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenACacheReleasedWhileLoading_WhenTheLoadFinishes_ThenNothingIsCached()
    {
        var reader = new FakeDiffReader();
        var first = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        var second = reader.Add(first, FakeDiffReader.FileAdding("a.txt", 1));
        using var store = CreateStore(reader, delay: (_, _) => Task.CompletedTask);

        store.Load(second, first, [first]);
        store.ReleaseCache();
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenACachedCommit_WhenItIsSelectedAgain_ThenNoGitIsNeeded()
    {
        var reader = new FakeDiffReader();
        var first = reader.Add(null, FakeDiffReader.FileAdding("a.txt", ["one"]));
        var second = reader.Add(first, FakeDiffReader.FileAdding("a.txt", ["two"]));
        using var store = CreateStore(reader);
        await LoadFakeAsync(store, second, first);
        store.Load(first, null);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded && store.Commit == first);
        var reads = reader.ReadCount;

        var started = store.Load(second, first);

        Assert.True(started);
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Equal(reads, reader.ReadCount);
        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "two" });
    }

    [Fact]
    public async Task GivenCachedDiffs_WhenReleasingTheCache_ThenTheShownDiffStaysAndTheCacheIsEmpty()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        using var store = CreateStore(reader);
        await LoadFakeAsync(store, commit);
        Assert.Equal(1, store.CachedCount);

        store.ReleaseCache();

        Assert.Equal(0, store.CachedCount);
        Assert.Equal(DiffStatus.Loaded, store.Status);
    }

    private static Task LoadedAsync(DiffStore store, ObjectId commit) =>
        WaitUntilAsync(() => store.Commit == commit && store.Status == DiffStatus.Loaded);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the store");
            await Task.Delay(10, Token);
        }
    }

    private static ObjectId IdOf(TestRepository testRepository, string revision)
    {
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", revision).Trim(), out var id));
        return id;
    }

    private static DiffStore CreateStore(FakeDiffReader reader, int rowLimit = DiffStore.DefaultRowLimit, Func<TimeSpan, CancellationToken, Task>? delay = null) =>
        new(reader, FakeCommitDetailsReader.Location, action => action(), rowLimit, delay: delay);

    private static async Task<DiffDocument> LoadFakeAsync(DiffStore store, ObjectId commit, ObjectId? firstParent = null)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Changed += () =>
        {
            if (store.Status is DiffStatus.Loaded or DiffStatus.Failed)
            {
                finished.TrySetResult();
            }
        };
        store.Load(commit, firstParent);
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(60), Token);
        return store.Document;
    }

    private static async Task<DiffStore> CreateStoreAsync(TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        return new DiffStore(TestGit.Services.Diff, location, action => action());
    }

    private static async Task<DiffDocument> LoadAsync(DiffStore store, TestRepository testRepository, string revision, string? parentRevision)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Changed += () =>
        {
            if (store.Status is DiffStatus.Loaded or DiffStatus.Failed)
            {
                finished.TrySetResult();
            }
        };
        store.Load(IdOf(testRepository, revision), parentRevision is null ? null : IdOf(testRepository, parentRevision));
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(60), Token);
        return store.Document;
    }
}
