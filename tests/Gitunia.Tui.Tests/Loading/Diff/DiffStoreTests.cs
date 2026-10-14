using Gitunia.Core.Diff;
using Gitunia.Git;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Diff;
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
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("big.txt", "start\n"));
        testRepository.Commit("big", new TestFile("big.txt", string.Concat(Enumerable.Range(0, 2000).Select(index => $"line {index}\n"))), new TestFile("other.txt", "x\n"));
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var store = new DiffStore(TestGit.Services.Diff, location, action => action(), rowLimit: 100);

        var document = await LoadAsync(store, testRepository, "HEAD", "HEAD~1");

        Assert.Contains("cut short", document.Rows[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheSameCommit_WhenLoadingAgain_ThenNothingIsStarted()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\n"));
        using var store = await CreateStoreAsync(testRepository);
        var commit = IdOf(testRepository, "HEAD");
        await LoadAsync(store, testRepository, "HEAD", null);

        Assert.False(store.Load(commit, null));
        Assert.Equal(DiffStatus.Loaded, store.Status);
    }

    [Fact]
    public async Task GivenALoadedDiff_WhenClearing_ThenTheStoreIsEmptyAgain()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\n"));
        using var store = await CreateStoreAsync(testRepository);
        await LoadAsync(store, testRepository, "HEAD", null);

        store.Clear();

        Assert.Equal(DiffStatus.None, store.Status);
        Assert.Null(store.Commit);
        Assert.Empty(store.Document.Rows);
    }

    [Fact]
    public async Task GivenACommitGitDoesNotKnow_WhenLoading_ThenTheLoadFailsWithAMessage()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\n"));
        using var store = await CreateStoreAsync(testRepository);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Changed += () =>
        {
            if (store.Status == DiffStatus.Failed)
            {
                changed.TrySetResult();
            }
        };

        Assert.True(ObjectId.TryParse(new string('a', 40), out var missing));
        store.Load(missing, null);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.Equal(DiffStatus.Failed, store.Status);
        Assert.Contains("Could not load the diff", store.Document.Rows[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNeighbours_WhenTheDiffIsShown_ThenTheyArePrefetchedAndShownAtOnce()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("a.txt", "one\n"))
            .Commit("second", new TestFile("a.txt", "two\n"))
            .Commit("third", new TestFile("a.txt", "three\n"));
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var store = new DiffStore(TestGit.Services.Diff, location, action => action(), delay: (_, _) => Task.CompletedTask);
        var changed = 0;
        store.Changed += () => changed++;
        var neighbours = new[] { IdOf(testRepository, "HEAD~1"), IdOf(testRepository, "HEAD~2") };
        store.Load(IdOf(testRepository, "HEAD"), IdOf(testRepository, "HEAD~1"), neighbours);

        await WaitUntilAsync(() => store.CachedCount == 3);
        var before = changed;
        var started = store.Load(neighbours[0], IdOf(testRepository, "HEAD~2"));

        Assert.True(started);
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Equal(before + 1, changed);
        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "two" });
    }

    [Fact]
    public async Task GivenAPrefetchWaiting_WhenAnotherCommitIsSelected_ThenTheStaleNeighboursAreNotCached()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("a.txt", "one\n"))
            .Commit("second", new TestFile("a.txt", "two\n"))
            .Commit("third", new TestFile("a.txt", "three\n"));
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var gate = new TaskCompletionSource();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var store = new DiffStore(TestGit.Services.Diff, location, action => action(), delay: async (_, token) =>
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
        store.Load(IdOf(testRepository, "HEAD"), IdOf(testRepository, "HEAD~1"), [IdOf(testRepository, "HEAD~1")]);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded);

        store.Load(IdOf(testRepository, "HEAD~2"), null);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded && store.Commit == IdOf(testRepository, "HEAD~2"));

        Assert.Equal(2, store.CachedCount);
        store.Clear();
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenANeighbourOverThePrefetchRowLimit_WhenPrefetching_ThenItIsSkippedAndTheNextOneIsRead()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("a.txt", "one\n"))
            .Commit("big", new TestFile("big.txt", string.Concat(Enumerable.Range(0, DiffStore.PrefetchRowLimit + 100).Select(index => $"line {index}\n"))))
            .Commit("small", new TestFile("a.txt", "two\n"))
            .Commit("tip", new TestFile("a.txt", "three\n"));
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var store = new DiffStore(TestGit.Services.Diff, location, action => action(), delay: (_, _) => Task.CompletedTask);

        store.Load(IdOf(testRepository, "HEAD"), IdOf(testRepository, "HEAD~1"), [IdOf(testRepository, "HEAD~2"), IdOf(testRepository, "HEAD~1")]);
        await WaitUntilAsync(() => store.CachedCount == 2);
        await LoadedAsync(store, IdOf(testRepository, "HEAD"));

        Assert.Equal(2, store.CachedCount);
        Assert.True(store.Load(IdOf(testRepository, "HEAD~1"), IdOf(testRepository, "HEAD~2")));
        Assert.Equal(DiffStatus.Loaded, store.Status);
    }

    [Fact]
    public async Task GivenACutDiff_WhenItLoads_ThenItIsNotCached()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("big.txt", "start\n"));
        testRepository.Commit("big", new TestFile("big.txt", string.Concat(Enumerable.Range(0, 2000).Select(index => $"line {index}\n"))));
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var store = new DiffStore(TestGit.Services.Diff, location, action => action(), rowLimit: 100);

        await LoadAsync(store, testRepository, "HEAD", "HEAD~1");

        Assert.True(store.IsCut);
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenACacheReleasedWhileLoading_WhenTheLoadFinishes_ThenNothingIsCached()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\n")).Commit("second", new TestFile("a.txt", "two\n"));
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var store = new DiffStore(TestGit.Services.Diff, location, action => action(), delay: (_, _) => Task.CompletedTask);

        store.Load(IdOf(testRepository, "HEAD"), IdOf(testRepository, "HEAD~1"), [IdOf(testRepository, "HEAD~1")]);
        store.ReleaseCache();
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenACachedCommit_WhenItIsSelectedAgain_ThenNoGitIsNeeded()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\n")).Commit("second", new TestFile("a.txt", "two\n"));
        using var store = await CreateStoreAsync(testRepository);
        await LoadAsync(store, testRepository, "HEAD", "HEAD~1");
        store.Load(IdOf(testRepository, "HEAD~1"), null);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded && store.Commit == IdOf(testRepository, "HEAD~1"));

        var started = store.Load(IdOf(testRepository, "HEAD"), IdOf(testRepository, "HEAD~1"));

        Assert.True(started);
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "two" });
    }

    [Fact]
    public async Task GivenCachedDiffs_WhenReleasingTheCache_ThenTheShownDiffStaysAndTheCacheIsEmpty()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "one\n"));
        using var store = await CreateStoreAsync(testRepository);
        await LoadAsync(store, testRepository, "HEAD", null);
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
