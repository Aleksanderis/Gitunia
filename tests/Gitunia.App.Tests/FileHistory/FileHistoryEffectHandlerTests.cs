using System.Runtime.CompilerServices;
using Gitunia.App.FileHistory;
using Gitunia.App.Tests.Tags;
using Gitunia.Core.Diff;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.History;
using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.FileHistory;

// Real git on temporary repositories: the handler is the only place that reads file histories.
public class FileHistoryEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenARenamedFile_WhenReadingItsHistory_ThenTheLoadedResultListsTheCommitsAcrossTheRename()
    {
        using var repository = TestRepositories.Linear();
        repository.Commit("add", new TestFile("old.txt", "a\nb\nc\nd\n"));
        repository.Git("mv", "old.txt", "new.txt");
        repository.Git("commit", "-q", "-m", "rename");
        var location = await LocateAsync(repository);
        var dispatcher = new RecordingDispatcher();

        new FileHistoryEffectHandler(TestGit.Services.FileHistory, TestGit.Services.Diff, dispatcher)
            .Handle(new ReadFileHistoryEffect(Tab, location, "HEAD", "new.txt", 4), Token);
        var result = await dispatcher.NextAsync(Token);

        var loaded = Assert.IsType<FileHistoryLoaded>(result);
        Assert.Equal(4, loaded.Generation);
        Assert.Equal(Tab, loaded.TabId);
        Assert.Equal(["rename", "add"], loaded.Entries.Select(entry => entry.Subject));
        Assert.Equal("old.txt", loaded.Entries[1].Path);
    }

    [Fact]
    public async Task GivenACommit_WhenReadingItsDiff_ThenTheLoadedResultHoldsTheRowsOfThatFileOnly()
    {
        using var repository = TestRepositories.Linear();
        repository.Commit("two files", new TestFile("a.txt", "one\n"), new TestFile("b.txt", "other\n"));
        var location = await LocateAsync(repository);
        var entry = new FileHistoryEntry(repository.Head, "T", "2026-01-01", "two files", "a.txt", null, FileChangeKind.Added);
        var dispatcher = new RecordingDispatcher();

        new FileHistoryEffectHandler(TestGit.Services.FileHistory, TestGit.Services.Diff, dispatcher)
            .Handle(new ReadFileHistoryDiffEffect(Tab, location, entry, 9), Token);
        var result = await dispatcher.NextAsync(Token);

        var loaded = Assert.IsType<FileHistoryDiffLoaded>(result);
        Assert.Equal(9, loaded.Generation);
        Assert.Contains(loaded.Document.Rows, row => row.Kind == DiffRowKind.Added && row.Text == "one");
        Assert.DoesNotContain(loaded.Document.Rows, row => row.Text == "other");
    }

    [Fact]
    public async Task GivenACommitWithNothingToShowForTheFile_WhenReadingItsDiff_ThenTheDocumentSaysSo()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        var entry = new FileHistoryEntry(repository.Head, "T", "2026-01-01", "x", "missing.txt", null, FileChangeKind.Modified);
        var dispatcher = new RecordingDispatcher();

        new FileHistoryEffectHandler(TestGit.Services.FileHistory, TestGit.Services.Diff, dispatcher)
            .Handle(new ReadFileHistoryDiffEffect(Tab, location, entry, 1), Token);
        var result = await dispatcher.NextAsync(Token);

        var loaded = Assert.IsType<FileHistoryDiffLoaded>(result);
        var row = Assert.Single(loaded.Document.Rows);
        Assert.Equal(FileHistoryEffectHandler.NoChangesText, row.Text);
    }

    [Fact]
    public async Task GivenALineRange_WhenReadingTheList_ThenTheLoadedResultHoldsTheCommitsOfThoseLinesAndTheRange()
    {
        using var repository = TestRepositories.Linear();
        repository.Commit("add", new TestFile("f.txt", "a\nb\nc\n"));
        repository.Commit("edit b", new TestFile("f.txt", "a\nB\nc\n"));
        repository.Commit("edit c", new TestFile("f.txt", "a\nB\nC\n"));
        var location = await LocateAsync(repository);
        var dispatcher = new RecordingDispatcher();
        var range = new LineRange(2, 2);

        new FileHistoryEffectHandler(TestGit.Services.FileHistory, TestGit.Services.Diff, dispatcher)
            .Handle(new ReadFileHistoryEffect(Tab, location, "HEAD", "f.txt", 3, range), Token);
        var result = await dispatcher.NextAsync(Token);

        var loaded = Assert.IsType<FileHistoryLoaded>(result);
        Assert.Equal(range, loaded.Range);
        Assert.Equal(["edit b", "add"], loaded.Entries.Select(entry => entry.Subject));
    }

    [Fact]
    public async Task GivenALineHistoryEntry_WhenReadingItsDiff_ThenTheRowsComeFromItsPatchWithoutRunningGit()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        const string patch = "diff --git a/f.txt b/f.txt\n--- a/f.txt\n+++ b/f.txt\n@@ -2,1 +2,1 @@\n-b\n+B";
        var entry = new FileHistoryEntry("abc", "T", "2026-01-01", "x", "f.txt", null, FileChangeKind.Modified, patch);
        var dispatcher = new RecordingDispatcher();

        new FileHistoryEffectHandler(new FailingReader(), new BlockingThenFastDiffReader { Fails = true }, dispatcher)
            .Handle(new ReadFileHistoryDiffEffect(Tab, location, entry, 3), Token);
        var result = await dispatcher.NextAsync(Token);

        var loaded = Assert.IsType<FileHistoryDiffLoaded>(result);
        Assert.Contains(loaded.Document.Rows, row => row.Kind == DiffRowKind.Added && row.Text == "B");
        Assert.Contains(loaded.Document.Rows, row => row.Kind == DiffRowKind.Removed && row.Text == "b");
    }

    [Fact]
    public async Task GivenAFailingReader_WhenReadingTheList_ThenTheFailureCarriesThePathAndTheReason()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        var dispatcher = new RecordingDispatcher();

        new FileHistoryEffectHandler(new FailingReader(), TestGit.Services.Diff, dispatcher)
            .Handle(new ReadFileHistoryEffect(Tab, location, "HEAD", "a.txt", 1), Token);
        var result = await dispatcher.NextAsync(Token);

        var failed = Assert.IsType<FileHistoryReadFailed>(result);
        Assert.StartsWith("Could not read the history of a.txt:", failed.Text, StringComparison.Ordinal);
        Assert.Contains("boom", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReadingTheList_ThenTheResultIsCancellationNotAFailure()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var dispatcher = new RecordingDispatcher();

        new FileHistoryEffectHandler(new CancellingReader(), TestGit.Services.Diff, dispatcher)
            .Handle(new ReadFileHistoryEffect(Tab, location, "HEAD", "a.txt", 1), source.Token);
        var result = await dispatcher.NextAsync(Token);

        Assert.IsType<FileHistoryReadCancelled>(result);
    }

    [Fact]
    public async Task GivenADiffReadStillRunning_WhenANewerOneStarts_ThenTheOlderIsCancelledAndTheNewerCompletes()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        var entry = new FileHistoryEntry(repository.Head, "T", "2026-01-01", "x", "a.txt", null, FileChangeKind.Modified);
        var dispatcher = new RecordingDispatcher();
        var diffs = new BlockingThenFastDiffReader();
        var handler = new FileHistoryEffectHandler(TestGit.Services.FileHistory, diffs, dispatcher);

        handler.Handle(new ReadFileHistoryDiffEffect(Tab, location, entry, 1), Token);
        await diffs.FirstStarted.Task.WaitAsync(Token);
        handler.Handle(new ReadFileHistoryDiffEffect(Tab, location, entry, 2), Token);
        var results = new[] { await dispatcher.NextAsync(Token), await dispatcher.NextAsync(Token) };

        Assert.Single(results.OfType<FileHistoryDiffCancelled>());
        Assert.Equal(2, Assert.Single(results.OfType<FileHistoryDiffLoaded>()).Generation);
    }

    [Fact]
    public async Task GivenAFailingDiffReader_WhenReadingTheDiff_ThenTheFailureEchoesTheGeneration()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        var entry = new FileHistoryEntry(repository.Head, "T", "2026-01-01", "x", "a.txt", null, FileChangeKind.Modified);
        var dispatcher = new RecordingDispatcher();

        new FileHistoryEffectHandler(TestGit.Services.FileHistory, new BlockingThenFastDiffReader { Fails = true }, dispatcher)
            .Handle(new ReadFileHistoryDiffEffect(Tab, location, entry, 5), Token);
        var result = await dispatcher.NextAsync(Token);

        var failed = Assert.IsType<FileHistoryDiffFailed>(result);
        Assert.Equal(5, failed.Generation);
        Assert.Contains("boom", failed.Text, StringComparison.Ordinal);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

    private sealed class FailingReader : IFileHistoryReader
    {
        public Task<ParseResult<FileHistoryEntry>> ReadAsync(
            RepositoryLocation location,
            string revision,
            string path,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromException<ParseResult<FileHistoryEntry>>(new InvalidOperationException("boom"));

        public Task<ParseResult<FileHistoryEntry>> ReadRangeAsync(
            RepositoryLocation location,
            string revision,
            string path,
            LineRange range,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromException<ParseResult<FileHistoryEntry>>(new InvalidOperationException("boom"));
    }

    private sealed class CancellingReader : IFileHistoryReader
    {
        public Task<ParseResult<FileHistoryEntry>> ReadAsync(
            RepositoryLocation location,
            string revision,
            string path,
            int limit,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new ParseResult<FileHistoryEntry>([], []));
        }

        public Task<ParseResult<FileHistoryEntry>> ReadRangeAsync(
            RepositoryLocation location,
            string revision,
            string path,
            LineRange range,
            int limit,
            CancellationToken cancellationToken) =>
            ReadAsync(location, revision, path, limit, cancellationToken);
    }

    // The first read waits until it is cancelled; later ones end at once; or every read fails.
    private sealed class BlockingThenFastDiffReader : IDiffReader
    {
        private int _calls;

        public bool Fails { get; init; }

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<DiffFile> ReadAsync(
            RepositoryLocation location,
            IReadOnlyList<string> command,
            ICollection<string>? warnings = null,
            int maximumLinesPerFile = int.MaxValue,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (Fails)
            {
                throw new InvalidOperationException("boom");
            }

            if (Interlocked.Increment(ref _calls) == 1)
            {
                FirstStarted.SetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public IAsyncEnumerable<DiffFile> ReadCommitAsync(
            RepositoryLocation location,
            ObjectId commit,
            ObjectId? firstParent,
            ICollection<string>? warnings = null,
            int maximumLinesPerFile = int.MaxValue,
            DiffOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<DiffFile> ReadCommitAgainstFirstParentAsync(
            RepositoryLocation location,
            ObjectId commit,
            ICollection<string>? warnings = null,
            int maximumLinesPerFile = int.MaxValue,
            DiffOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<DiffFile> ReadWorkingFileAsync(
            RepositoryLocation location,
            WorkingDiffSide side,
            StatusEntry entry,
            ICollection<string>? warnings = null,
            int maximumLinesPerFile = int.MaxValue,
            DiffOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

    }
}
