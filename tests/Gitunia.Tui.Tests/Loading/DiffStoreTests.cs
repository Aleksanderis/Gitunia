using Gitunia.Core.Diff;
using Gitunia.Git;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading;
using Xunit;

namespace Gitunia.Tui.Tests.Loading;

public class DiffStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAModifiedFile_WhenLoading_ThenRowsShowTheChange()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "one\ntwo\n")).Commit("edit", ("a.txt", "one\nTWO\n"));
        using var store = await CreateStoreAsync(repository);

        var document = await LoadAsync(store, repository, "HEAD", "HEAD~1");

        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Removed, Text: "two" });
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "TWO" });
    }

    [Fact]
    public async Task GivenARootCommit_WhenLoading_ThenEverythingIsAdded()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "one\n"));
        using var store = await CreateStoreAsync(repository);

        var document = await LoadAsync(store, repository, "HEAD", null);

        Assert.Equal("A a.txt", document.Rows[document.FileRows[0]].Text);
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "one" });
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenLoading_ThenItIsComparedWithTheFirstParent()
    {
        using var repository = TestRepository.Create()
            .Commit("root", ("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", ("side.txt", "side\n"))
            .Checkout("main")
            .Commit("main work", ("main.txt", "main\n"))
            .Merge("side");
        using var store = await CreateStoreAsync(repository);

        var document = await LoadAsync(store, repository, "HEAD", "HEAD^1");

        var paths = document.FilePaths.Select(names => names[0]).ToArray();
        Assert.Equal(["side.txt"], paths);
    }

    [Fact]
    public async Task GivenARenameAndABinaryFile_WhenLoading_ThenBothAreDescribed()
    {
        using var repository = TestRepository.Create()
            .Commit("root", ("old.txt", "line one\nline two\nline three\nline four\n"), ("img.bin", "a\0b"));
        repository.Git("mv", "old.txt", "new.txt");
        repository.WriteFile("img.bin", "a\0c\0d");
        repository.Git("add", "-A");
        repository.Git("commit", "-q", "-m", "rename and change binary");
        using var store = await CreateStoreAsync(repository);

        var document = await LoadAsync(store, repository, "HEAD", "HEAD~1");

        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.FileHeader, Text: "R100 old.txt -> new.txt" });
        Assert.Contains(document.Rows, row => row is { Kind: DiffRowKind.Note, Text: "Binary file, content not shown" });
    }

    [Fact]
    public async Task GivenAFiftyThousandLineDiff_WhenLoading_ThenAllRowsArriveAndAnchorsAreSorted()
    {
        using var repository = TestRepository.Create().Commit("root", ("big.txt", "start\n"));
        repository.Commit("big", ("big.txt", string.Concat(Enumerable.Range(0, 50_000).Select(index => $"line {index}\n"))));
        using var store = await CreateStoreAsync(repository);

        var document = await LoadAsync(store, repository, "HEAD", "HEAD~1");

        Assert.True(document.Rows.Count > 50_000);
        Assert.Single(document.HunkRows);
        Assert.Equal(document.HunkRows.Order(), document.HunkRows);
    }

    [Fact]
    public async Task GivenARowLimit_WhenTheDiffIsLonger_ThenItIsCutShortWithANote()
    {
        using var repository = TestRepository.Create().Commit("root", ("big.txt", "start\n"));
        repository.Commit("big", ("big.txt", string.Concat(Enumerable.Range(0, 2000).Select(index => $"line {index}\n"))), ("other.txt", "x\n"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new DiffStore(repo, action => action(), rowLimit: 100);

        var document = await LoadAsync(store, repository, "HEAD", "HEAD~1");

        Assert.Contains("cut short", document.Rows[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheSameCommit_WhenLoadingAgain_ThenNothingIsStarted()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "one\n"));
        using var store = await CreateStoreAsync(repository);
        var commit = IdOf(repository, "HEAD");
        await LoadAsync(store, repository, "HEAD", null);

        Assert.False(store.Load(commit, null));
        Assert.Equal(DiffStatus.Loaded, store.Status);
    }

    [Fact]
    public async Task GivenALoadedDiff_WhenClearing_ThenTheStoreIsEmptyAgain()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "one\n"));
        using var store = await CreateStoreAsync(repository);
        await LoadAsync(store, repository, "HEAD", null);

        store.Clear();

        Assert.Equal(DiffStatus.None, store.Status);
        Assert.Null(store.Commit);
        Assert.Empty(store.Document.Rows);
    }

    [Fact]
    public async Task GivenACommitGitDoesNotKnow_WhenLoading_ThenTheLoadFailsWithAMessage()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "one\n"));
        using var store = await CreateStoreAsync(repository);
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
        using var repository = TestRepository.Create()
            .Commit("root", ("a.txt", "one\n"))
            .Commit("second", ("a.txt", "two\n"))
            .Commit("third", ("a.txt", "three\n"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new DiffStore(repo, action => action(), delay: (_, _) => Task.CompletedTask);
        var changed = 0;
        store.Changed += () => changed++;
        var neighbours = new[] { IdOf(repository, "HEAD~1"), IdOf(repository, "HEAD~2") };
        store.Load(IdOf(repository, "HEAD"), IdOf(repository, "HEAD~1"), neighbours);

        await WaitUntilAsync(() => store.CachedCount == 3);
        var before = changed;
        var started = store.Load(neighbours[0], IdOf(repository, "HEAD~2"));

        Assert.True(started);
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Equal(before + 1, changed);
        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "two" });
    }

    [Fact]
    public async Task GivenAPrefetchWaiting_WhenAnotherCommitIsSelected_ThenTheStaleNeighboursAreNotCached()
    {
        using var repository = TestRepository.Create()
            .Commit("root", ("a.txt", "one\n"))
            .Commit("second", ("a.txt", "two\n"))
            .Commit("third", ("a.txt", "three\n"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var gate = new TaskCompletionSource();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var store = new DiffStore(repo, action => action(), delay: async (_, token) =>
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
        store.Load(IdOf(repository, "HEAD"), IdOf(repository, "HEAD~1"), [IdOf(repository, "HEAD~1")]);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded);

        store.Load(IdOf(repository, "HEAD~2"), null);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded && store.Commit == IdOf(repository, "HEAD~2"));

        Assert.Equal(2, store.CachedCount);
        store.Clear();
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenANeighbourOverThePrefetchRowLimit_WhenPrefetching_ThenItIsSkippedAndTheNextOneIsRead()
    {
        using var repository = TestRepository.Create()
            .Commit("root", ("a.txt", "one\n"))
            .Commit("big", ("big.txt", string.Concat(Enumerable.Range(0, DiffStore.PrefetchRowLimit + 100).Select(index => $"line {index}\n"))))
            .Commit("small", ("a.txt", "two\n"))
            .Commit("tip", ("a.txt", "three\n"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new DiffStore(repo, action => action(), delay: (_, _) => Task.CompletedTask);

        store.Load(IdOf(repository, "HEAD"), IdOf(repository, "HEAD~1"), [IdOf(repository, "HEAD~2"), IdOf(repository, "HEAD~1")]);
        await WaitUntilAsync(() => store.CachedCount == 2);
        await LoadedAsync(store, IdOf(repository, "HEAD"));

        Assert.Equal(2, store.CachedCount);
        Assert.True(store.Load(IdOf(repository, "HEAD~1"), IdOf(repository, "HEAD~2")));
        Assert.Equal(DiffStatus.Loaded, store.Status);
    }

    [Fact]
    public async Task GivenACutDiff_WhenItLoads_ThenItIsNotCached()
    {
        using var repository = TestRepository.Create().Commit("root", ("big.txt", "start\n"));
        repository.Commit("big", ("big.txt", string.Concat(Enumerable.Range(0, 2000).Select(index => $"line {index}\n"))));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new DiffStore(repo, action => action(), rowLimit: 100);

        await LoadAsync(store, repository, "HEAD", "HEAD~1");

        Assert.True(store.IsCut);
        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenACacheReleasedWhileLoading_WhenTheLoadFinishes_ThenNothingIsCached()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "one\n")).Commit("second", ("a.txt", "two\n"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new DiffStore(repo, action => action(), delay: (_, _) => Task.CompletedTask);

        store.Load(IdOf(repository, "HEAD"), IdOf(repository, "HEAD~1"), [IdOf(repository, "HEAD~1")]);
        store.ReleaseCache();
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal(0, store.CachedCount);
    }

    [Fact]
    public async Task GivenACachedCommit_WhenItIsSelectedAgain_ThenNoGitIsNeeded()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "one\n")).Commit("second", ("a.txt", "two\n"));
        using var store = await CreateStoreAsync(repository);
        await LoadAsync(store, repository, "HEAD", "HEAD~1");
        store.Load(IdOf(repository, "HEAD~1"), null);
        await WaitUntilAsync(() => store.Status == DiffStatus.Loaded && store.Commit == IdOf(repository, "HEAD~1"));

        var started = store.Load(IdOf(repository, "HEAD"), IdOf(repository, "HEAD~1"));

        Assert.True(started);
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "two" });
    }

    [Fact]
    public async Task GivenCachedDiffs_WhenReleasingTheCache_ThenTheShownDiffStaysAndTheCacheIsEmpty()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "one\n"));
        using var store = await CreateStoreAsync(repository);
        await LoadAsync(store, repository, "HEAD", null);
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

    private static ObjectId IdOf(TestRepository repository, string revision)
    {
        Assert.True(ObjectId.TryParse(repository.Git("rev-parse", revision).Trim(), out var id));
        return id;
    }

    private static async Task<DiffStore> CreateStoreAsync(TestRepository repository)
    {
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        return new DiffStore(repo, action => action());
    }

    private static async Task<DiffDocument> LoadAsync(DiffStore store, TestRepository repository, string revision, string? parentRevision)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Changed += () =>
        {
            if (store.Status is DiffStatus.Loaded or DiffStatus.Failed)
            {
                finished.TrySetResult();
            }
        };
        store.Load(IdOf(repository, revision), parentRevision is null ? null : IdOf(repository, parentRevision));
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(60), Token);
        return store.Document;
    }
}
