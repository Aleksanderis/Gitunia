using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap;
using Gitunia.App.Loading;
using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Gitunia.Git;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Loading.Diff;

public class DiffStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAModifiedFile_WhenLoading_ThenRowsShowTheChange()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\ntwo\n")).Commit("edit", new TestFile("a.txt", "one\nTWO\n"));
        var pump = new UiPump(Token);
        using var store = await CreateStoreAsync(pump, testRepository);

        var document = await LoadAsync(pump, store, testRepository, "HEAD", "HEAD~1");

        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Removed, Text: "two" });
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "TWO" });
    }

    [Fact]
    public async Task GivenARootCommit_WhenLoading_ThenEverythingIsAdded()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\n"));
        var pump = new UiPump(Token);
        using var store = await CreateStoreAsync(pump, testRepository);

        var document = await LoadAsync(pump, store, testRepository, "HEAD", null);

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
        var pump = new UiPump(Token);
        using var store = await CreateStoreAsync(pump, testRepository);

        var document = await LoadAsync(pump, store, testRepository, "HEAD", "HEAD^1");

        var paths = document.FilePaths.Select(names => names[0]).ToArray();
        Assert.Equal(["side.txt"], paths);
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenSwitchingToTheSecondParent_ThenTheDiffIsAgainstItAndTheCacheKeepsTheFirst()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", new TestFile("side.txt", "side\n"))
            .Checkout("main")
            .Commit("main work", new TestFile("main.txt", "main\n"))
            .Merge("side");
        var pump = new UiPump(Token);
        using var store = await CreateStoreAsync(pump, testRepository);
        var merge = IdOf(testRepository, "HEAD");
        await LoadAsync(pump, store, testRepository, "HEAD", "HEAD^1");

        store.Load(merge, IdOf(testRepository, "HEAD^2"), isAlternateParent: true);
        var secondParent = IdOf(testRepository, "HEAD^2");
        await pump.RunUntilAsync(() => store.Status is DiffStatus.Loaded && store.IsShowing(merge, secondParent));
        var second = store.Document.FilePaths.Select(names => names[0]).ToArray();
        var again = await LoadAsync(pump, store, testRepository, "HEAD", "HEAD^1");

        Assert.Equal(["main.txt"], second);
        Assert.Equal(["side.txt"], [.. again.FilePaths.Select(names => names[0])]);
        Assert.Equal(1, store.CachedCount);
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
        var pump = new UiPump(Token);
        using var store = await CreateStoreAsync(pump, testRepository);

        var document = await LoadAsync(pump, store, testRepository, "HEAD", "HEAD~1");

        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.FileHeader, Text: "R old.txt -> new.txt" });
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Note, Text: "Binary file, content not shown" });
    }

    [Fact]
    public async Task GivenAFiftyThousandLineDiff_WhenLoading_ThenAllRowsArriveAndAnchorsAreSorted()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("big.txt", "start\n"));
        testRepository.Commit("big", new TestFile("big.txt", string.Concat(Enumerable.Range(0, 50_000).Select(index => $"line {index}\n"))));
        var pump = new UiPump(Token);
        using var store = await CreateStoreAsync(pump, testRepository);

        var document = await LoadAsync(pump, store, testRepository, "HEAD", "HEAD~1");

        Assert.True(document.Rows.Count > 50_000);
        Assert.Single(document.HunkRows);
        Assert.Equal(document.HunkRows.Order(), document.HunkRows);
    }

    [Fact]
    public async Task GivenARowLimit_WhenTheDiffIsLonger_ThenItIsCutShortWithANote()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("big.txt", 2000), FakeDiffReader.FileAdding("other.txt", 1));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader, rowLimit: 100);

        var document = await LoadFakeAsync(pump, store, commit);

        Assert.Contains("cut short", document.Rows[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARemappedLoadAllKey_WhenTheDiffIsCutShort_ThenTheNoteNamesTheNewKey()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("big.txt", 2000));
        var pump = new UiPump(Token);
        var keymap = new KeyBindingTable(
        [
            new KeyBinding(KeyMode.Normal, "Diff", KeySequenceParser.Parse("<space>zv"), ActionIds.DiffLoadAnyway),
        ]);
        using var store = new DiffStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump,
            new DiffStoreOptions(100, Keymap: keymap));

        var document = await LoadFakeAsync(pump, store, commit);

        Assert.Equal("Diff cut short after 100 rows: load all of it (<space>zv)", document.Rows[^1].Text);
    }

    [Fact]
    public async Task GivenTheSameCommit_WhenLoadingAgain_ThenNothingIsStarted()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        await LoadFakeAsync(pump, store, commit);

        Assert.False(store.Load(commit, null));
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task GivenALoadedDiff_WhenClearing_ThenTheStoreIsEmptyAgain()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        await LoadFakeAsync(pump, store, commit);

        store.Clear();

        Assert.Equal(DiffStatus.None, store.Status);
        Assert.Null(store.Commit);
        Assert.Empty(store.Document.Rows);
    }

    [Fact]
    public async Task GivenACommitGitDoesNotKnow_WhenLoading_ThenTheLoadFailsWithAMessage()
    {
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, new FakeDiffReader());

        await LoadFakeAsync(pump, store, FakeDiffReader.MissingId());

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
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader, delay: (_, _) => Task.CompletedTask);
        var changed = 0;
        store.Changed += () => changed++;
        store.Load(third, second, [second, first]);

        await pump.RunUntilAsync(() => store.CachedCount == 3);
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
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader, delay: async (_, token) =>
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
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        store.Load(first, null);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded && store.Commit == first);

        Assert.Equal(2, store.CachedCount);
        store.Clear();
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenCachedDiffs_WhenTheOptionsChange_ThenTheCacheIsDroppedAndTheCommitLoadsAgain()
    {
        var reader = new FakeDiffReader();
        var first = reader.Add(null, FakeDiffReader.FileAdding("a.txt", ["one"]));
        var second = reader.Add(first, FakeDiffReader.FileAdding("a.txt", ["two"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader, delay: (_, _) => Task.CompletedTask);
        store.Load(second, first, [first]);
        await pump.RunUntilAsync(() => store.CachedCount == 2);
        var reads = reader.ReadCount;

        var changed = store.SetOptions(DiffOptions.Default.MoreContext());
        var unchanged = store.SetOptions(DiffOptions.Default.MoreContext());

        Assert.True(changed);
        Assert.False(unchanged);
        Assert.Equal(DiffStatus.Loading, store.Status);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);
        Assert.True(reader.ReadCount > reads);
    }

    [Fact]
    public async Task GivenANeighbourOverThePrefetchRowLimit_WhenPrefetching_ThenItIsSkippedAndTheNextOneIsRead()
    {
        var reader = new FakeDiffReader();
        var root = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        var big = reader.Add(root, FakeDiffReader.FileAdding("big.txt", DiffStore.PrefetchRowLimit + 100));
        var small = reader.Add(big, FakeDiffReader.FileAdding("a.txt", 1));
        var tip = reader.Add(small, FakeDiffReader.FileAdding("a.txt", 1));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader, delay: (_, _) => Task.CompletedTask);

        store.Load(tip, small, [big, small]);
        await pump.RunUntilAsync(() => store.CachedCount == 2);

        Assert.Equal(2, store.CachedCount);
        Assert.True(store.Load(small, big));
        Assert.Equal(DiffStatus.Loaded, store.Status);
    }

    [Fact]
    public async Task GivenACutDiff_WhenItLoads_ThenItIsNotCached()
    {
        var reader = new FakeDiffReader();
        var commit = reader.Add(null, FakeDiffReader.FileAdding("big.txt", 2000));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader, rowLimit: 100);

        await LoadFakeAsync(pump, store, commit);

        Assert.True(store.IsCut);
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenACacheReleasedWhileLoading_WhenTheLoadFinishes_ThenNothingIsCached()
    {
        var reader = new FakeDiffReader();
        var first = reader.Add(null, FakeDiffReader.FileAdding("a.txt", 1));
        var second = reader.Add(first, FakeDiffReader.FileAdding("a.txt", 1));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader, delay: (_, _) => Task.CompletedTask);

        store.Load(second, first, [first]);
        store.ReleaseCache();
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenACachedCommit_WhenItIsSelectedAgain_ThenNoGitIsNeeded()
    {
        var reader = new FakeDiffReader();
        var first = reader.Add(null, FakeDiffReader.FileAdding("a.txt", ["one"]));
        var second = reader.Add(first, FakeDiffReader.FileAdding("a.txt", ["two"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        await LoadFakeAsync(pump, store, second, first);
        store.Load(first, null);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded && store.Commit == first);
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
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        await LoadFakeAsync(pump, store, commit);
        Assert.Equal(1, store.CachedCount);

        store.ReleaseCache();

        Assert.Equal(0, store.CachedCount);
        Assert.Equal(DiffStatus.Loaded, store.Status);
    }

    private static ObjectId IdOf(TestRepository testRepository, string revision)
    {
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", revision).Trim(), out var id));
        return id;
    }

    private static DiffStore CreateStore(UiPump pump, FakeDiffReader reader, int rowLimit = DiffStore.DefaultRowLimit, Func<TimeSpan, CancellationToken, Task>? delay = null) =>
        new(
            reader,
            FakeCommitDetailsReader.Location,
            pump,
            new DiffStoreOptions(rowLimit, Delayer: delay is null ? null : new DelegateDelayer(delay)));

    private static async Task<DiffDocument> LoadFakeAsync(UiPump pump, DiffStore store, ObjectId commit, ObjectId? firstParent = null)
    {
        store.Load(commit, firstParent);
        await pump.RunUntilAsync(() => store.Status is DiffStatus.Loaded or DiffStatus.Failed);
        return store.Document;
    }

    private static async Task<DiffStore> CreateStoreAsync(UiPump pump, TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        return new DiffStore(TestGit.Services.Diff, location, pump);
    }

    private static async Task<DiffDocument> LoadAsync(UiPump pump, DiffStore store, TestRepository testRepository, string revision, string? parentRevision)
    {
        store.Load(IdOf(testRepository, revision), parentRevision is null ? null : IdOf(testRepository, parentRevision));
        await pump.RunUntilAsync(() => store.Status is DiffStatus.Loaded or DiffStatus.Failed);
        return store.Document;
    }
}
