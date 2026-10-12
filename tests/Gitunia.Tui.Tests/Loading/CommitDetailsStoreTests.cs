using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading;
using Xunit;

namespace Gitunia.Tui.Tests.Loading;

public class CommitDetailsStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ObjectId IdOf(TestRepository repository, string revision)
    {
        Assert.True(ObjectId.TryParse(repository.Git("rev-parse", revision).Trim(), out var id));
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
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "1")).Commit("second", ("a.txt", "2"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new CommitDetailsStore(repo, action => action(), TimeSpan.FromMilliseconds(1));
        var head = IdOf(repository, "HEAD");

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
        using var repository = TestRepository.Create()
            .Commit("one", ("a.txt", "1"))
            .Commit("two", ("a.txt", "2"))
            .Commit("three", ("a.txt", "3"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new CommitDetailsStore(repo, action => action(), TimeSpan.FromMilliseconds(300));
        var seen = new List<(ObjectId? Commit, CommitDetailsStatus Status)>();
        store.Changed += () => seen.Add((store.Current.Commit, store.Current.Status));
        var first = IdOf(repository, "HEAD~2");
        var second = IdOf(repository, "HEAD~1");
        var last = IdOf(repository, "HEAD");

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
        using var repository = TestRepository.Create().Commit("one", ("a.txt", "1")).Commit("two", ("a.txt", "2"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new CommitDetailsStore(repo, action => action(), TimeSpan.FromMilliseconds(1));
        var first = IdOf(repository, "HEAD~1");
        var second = IdOf(repository, "HEAD");
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
        using var repository = TestRepository.Create().Commit("one", ("a.txt", "1"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        using var store = new CommitDetailsStore(repo, action => action(), TimeSpan.FromMilliseconds(1));
        Assert.True(ObjectId.TryParse(new string('1', 40), out var missing));

        store.Select(missing);
        var settled = await WaitForSettledAsync(store, missing);

        Assert.Equal(CommitDetailsStatus.Failed, settled.Status);
        Assert.NotEmpty(settled.Error);
    }

    [Fact]
    public async Task GivenDisposedStore_WhenLoadWasPending_ThenNothingIsPosted()
    {
        using var repository = TestRepository.Create().Commit("one", ("a.txt", "1"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var posted = 0;
        var store = new CommitDetailsStore(repo, action => { posted++; action(); }, TimeSpan.FromMilliseconds(200));
        store.Select(IdOf(repository, "HEAD"));

        store.Dispose();
        await Task.Delay(500, Token);

        Assert.Equal(0, posted);
    }
}
