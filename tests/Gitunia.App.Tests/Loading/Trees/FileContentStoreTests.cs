using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Trees;
using Gitunia.Git;
using Gitunia.Git.Blobs;
using Gitunia.Git.Blobs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.App.Tests.Loading.Trees;

public class FileContentStoreTests
{
    private static readonly ObjectId Commit = FakeObjectIds.Of(1);

    private readonly UiPump _pump = new(TestContext.Current.CancellationToken);

    [Fact]
    public async Task GivenAFile_WhenLoading_ThenItShowsLoadingAtOnceAndTheTextWhenItArrives()
    {
        var reader = new GatedBlobReader();
        using var store = new FileContentStore(reader, FakeCommitDetailsReader.Location, _pump.Post);
        var changes = 0;
        store.Changed += () => changes++;

        store.Load(Commit, "a.txt", FakeObjectIds.Of(2), 6);

        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
        Assert.Equal("Loading...", store.Current.Document.Rows[0].Text);
        await reader.WaitForReadsAsync(1);
        reader.Release("hi\n");
        await _pump.RunUntilAsync(() => changes == 1);
        Assert.Equal(CommitDetailsStatus.Loaded, store.Current.Status);
        Assert.Equal(["hi"], store.Current.Document.Rows.Select(row => row.Text));
    }

    [Fact]
    public async Task GivenASlowReadAndAnotherFile_WhenTheFirstArrivesLate_ThenOnlyTheSecondIsShown()
    {
        var reader = new GatedBlobReader();
        using var store = new FileContentStore(reader, FakeCommitDetailsReader.Location, _pump.Post);
        var changes = 0;
        store.Changed += () => changes++;
        store.Load(Commit, "first.txt", FakeObjectIds.Of(2), 1);
        await reader.WaitForReadsAsync(1);
        store.Load(Commit, "second.txt", FakeObjectIds.Of(3), 1);
        await reader.WaitForReadsAsync(2);

        reader.Release("late\n");
        reader.Release("second\n");
        await _pump.RunUntilAsync(() => changes >= 1);

        Assert.Equal("second.txt", store.Current.Path);
        Assert.Equal(["second"], store.Current.Document.Rows.Select(row => row.Text));
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task GivenAReadInFlight_WhenCleared_ThenTheResultIsDropped()
    {
        var reader = new GatedBlobReader();
        using var store = new FileContentStore(reader, FakeCommitDetailsReader.Location, _pump.Post);
        store.Load(Commit, "a.txt", FakeObjectIds.Of(2), 1);
        await reader.WaitForReadsAsync(1);

        store.Clear();
        reader.Release("x\n");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(FileContentState.Empty, store.Current);
    }

    [Fact]
    public async Task GivenAFailingRead_WhenLoading_ThenANoteNamesTheFailure()
    {
        using var store = new FileContentStore(new FailingBlobReader(), FakeCommitDetailsReader.Location, _pump.Post);
        store.Load(Commit, "a.txt", FakeObjectIds.Of(2), 1);

        await _pump.RunUntilAsync(() => store.Current.Status == CommitDetailsStatus.Failed);

        Assert.Equal("Cannot read the file: boom", store.Current.Document.Rows[0].Text);
        Assert.Equal("a.txt", store.Current.Document.ContentPath);
    }

    private sealed class GatedBlobReader : IBlobReader
    {
        private readonly List<TaskCompletionSource<string>> _all = [];
        private readonly object _lock = new();

        public async Task<BlobContent> ReadAsync(
            RepositoryLocation location,
            ObjectId blob,
            long? knownSize = null,
            CancellationToken cancellationToken = default)
        {
            var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                _all.Add(source);
            }

            var text = await source.Task.WaitAsync(cancellationToken);

            return new BlobContent(BlobKind.Text, text, text.Length);
        }

        public void Release(string text)
        {
            lock (_lock)
            {
                var next = _all.First(source => !source.Task.IsCompleted);
                next.SetResult(text);
            }
        }

        public async Task WaitForReadsAsync(int count)
        {
            while (true)
            {
                lock (_lock)
                {
                    if (_all.Count >= count)
                    {
                        return;
                    }
                }

                await Task.Delay(5, TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class FailingBlobReader : IBlobReader
    {
        public Task<BlobContent> ReadAsync(
            RepositoryLocation location,
            ObjectId blob,
            long? knownSize = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
    }
}
