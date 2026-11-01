using Gitunia.App.Discard;
using Gitunia.App.Staging;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Discard;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Discard;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.App.Tests.Discard;

// The handler is the only place that throws changes away. A fake discarder shows what git is asked; real git shows it works.
public class DiscardEffectHandlerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly RepositoryLocation Nowhere = new("/r", "/r/.git", "/r/.git", false, false);

    [Fact]
    public async Task GivenTrackedAndUntrackedFiles_WhenDiscardingTheBatch_ThenTheTrackedAreRestoredAndTheUntrackedDeleted()
    {
        var run = new Run();
        IReadOnlyList<StatusEntry> files = [Modified("a.txt"), Untracked("n.txt"), Modified("b.txt")];

        var result = await run.ExecuteAsync(new DiscardFilesEffect(Tab, Nowhere, files, 1));

        var success = Assert.IsType<DiscardSucceeded>(result);
        Assert.Equal("Discarded 3 files (1 untracked deleted, 1 skipped)", success.Text);
        Assert.Equal(["a.txt", "b.txt"], Assert.Single(run.Discarder.Restored));
        Assert.Equal(["n.txt"], Assert.Single(run.Discarder.Deleted));
        Assert.Equal(["refresh", "result"], run.Events);
    }

    [Fact]
    public async Task GivenGitFailsOnABatch_WhenDiscarding_ThenTheErrorMentionsTheTrackedFilesAndTheStatusIsStillRead()
    {
        var run = new Run();
        run.Discarder.Failure = new InvalidOperationException("index.lock exists");

        var result = await run.ExecuteAsync(new DiscardFilesEffect(Tab, Nowhere, [Modified("a.txt"), Modified("b.txt")], 0));

        var failed = Assert.IsType<DiscardFailed>(result);
        Assert.Contains("index.lock exists", failed.Text, StringComparison.Ordinal);
        Assert.Contains("may already be restored", failed.Text, StringComparison.Ordinal);
        Assert.Equal(["refresh", "result"], run.Events);
    }

    [Fact]
    public async Task GivenOneModifiedFile_WhenDiscardingIt_ThenItIsRestoredAndTheResultSaysSo()
    {
        var run = new Run();

        var result = await run.ExecuteAsync(WholeFile(Modified("a.txt")));

        Assert.Equal("Discarded all changes to a.txt", Assert.IsType<DiscardSucceeded>(result).Text);
        Assert.Equal(["a.txt"], Assert.Single(run.Discarder.Restored));
        Assert.Empty(run.Discarder.Deleted);
    }

    [Fact]
    public async Task GivenOneUntrackedFile_WhenDiscardingIt_ThenItIsDeletedAndNotRestored()
    {
        var run = new Run();

        var result = await run.ExecuteAsync(WholeFile(Untracked("new.txt")));

        Assert.Equal("Deleted untracked new.txt", Assert.IsType<DiscardSucceeded>(result).Text);
        Assert.Equal(["new.txt"], Assert.Single(run.Discarder.Deleted));
        Assert.Empty(run.Discarder.Restored);
    }

    [Fact]
    public async Task GivenGitFailsOnAFile_WhenDiscarding_ThenTheErrorIsTheResultAndTheStatusIsStillRead()
    {
        var run = new Run();
        run.Discarder.Failure = new InvalidOperationException("index.lock exists");

        var result = await run.ExecuteAsync(WholeFile(Modified("a.txt")));

        var failed = Assert.IsType<DiscardFailed>(result);
        Assert.StartsWith("Could not discard: index.lock exists", failed.Text, StringComparison.Ordinal);
        Assert.Equal(["refresh", "result"], run.Events);
    }

    [Fact]
    public async Task GivenACancelledTab_WhenDiscarding_ThenTheResultIsACancelAndNothingIsRead()
    {
        var run = new Run();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        run.Discarder.Failure = new OperationCanceledException(source.Token);

        var result = await run.ExecuteWithTokenAsync(WholeFile(Modified("a.txt")), source.Token);

        Assert.IsType<DiscardCancelled>(result);
        Assert.Equal(["result"], run.Events);
    }

    [Fact]
    public async Task GivenAHunk_WhenConfirmed_ThenAReversePatchOfJustThoseLinesGoesToTheWorkingTreeAndTheCursorMoves()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));
        var move = new DiffCursorMove(0, 1);

        var result = await run.ExecuteAsync(Lines([Added(2, "TWO")], true, false, move));

        var success = Assert.IsType<DiscardSucceeded>(result);
        Assert.Equal("Discarded 1 line of a.txt", success.Text);
        Assert.Equal(move, success.Move);
        Assert.Equal(
            "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,3 +1,4 @@\n one\n+TWO\n three\n four\n",
            Assert.Single(run.Discarder.ReversedPatches));
    }

    [Fact]
    public async Task GivenALineThatIsNoLongerInTheFile_WhenConfirmed_ThenNothingIsDiscardedAndTheCursorStays()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));

        var result = await run.ExecuteAsync(Lines([Added(2, "something else")], true, false, new DiffCursorMove(0, 1)));

        var success = Assert.IsType<DiscardSucceeded>(result);
        Assert.Equal(0, run.Discarder.CallCount);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
        Assert.Contains("changed since the diff was read", success.Text, StringComparison.Ordinal);
        Assert.Null(success.Move);
    }

    [Fact]
    public async Task GivenAFileWithInvalidText_WhenConfirmingAHunk_ThenTheWholeFileIsNeverDiscardedBehindTheDialog()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt") with { HasInvalidText = true });

        var result = await run.ExecuteAsync(Lines([Added(2, "TWO")], false, false, new DiffCursorMove(0, -1)));

        var success = Assert.IsType<DiscardSucceeded>(result);
        Assert.Equal(0, run.Discarder.CallCount);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
        Assert.Contains("nothing was discarded", success.Text, StringComparison.Ordinal);
        Assert.Null(success.Move);
    }

    [Fact]
    public async Task GivenAHunkOfAnUntrackedFile_WhenConfirmed_ThenTheWholeFileIsDeleted()
    {
        var run = new Run();

        var result = await run.ExecuteAsync(
            Lines([Added(1, "x")], true, false, new DiffCursorMove(0, -1), Untracked("new.txt")));

        Assert.Equal("Deleted untracked new.txt", Assert.IsType<DiscardSucceeded>(result).Text);
        Assert.Equal(["new.txt"], Assert.Single(run.Discarder.Deleted));
    }

    [Fact]
    public async Task GivenARealRepository_WhenDiscardingAFileAndAnUntrackedOne_ThenTheTreeIsBackAsTheIndexHasIt()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        repository.WriteFile("new.txt", "new\n");
        var location = await LocateAsync(repository);
        var run = new Run(TestGit.Services.Diff, TestGit.Services.Discarder);
        var tracked = Modified("file.txt");

        var result = await run.ExecuteAsync(new DiscardFilesEffect(Tab, location, [tracked, Untracked("new.txt")], 0));

        Assert.IsType<DiscardSucceeded>(result);
        Assert.Empty(repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task GivenARealRepository_WhenDiscardingOneLine_ThenOnlyThatLineIsGoneFromTheFileAndTheIndexIsUntouched()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "one\nchanged\nnext\n");
        var location = await LocateAsync(repository);
        var run = new Run(TestGit.Services.Diff, TestGit.Services.Discarder);
        var entry = Modified("file.txt");
        var files = await TestGit.Services.Diff
            .ReadWorkingFileAsync(
                location,
                WorkingDiffSide.Unstaged,
                entry,
                cancellationToken: TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);
        var line = files.SelectMany(file => file.Hunks).SelectMany(hunk => hunk.Lines)
            .First(candidate => candidate.Kind == DiffLineKind.Added);
        var picked = new ChangedLine(DiffLineKind.Added, line.NewLineNumber ?? 0, line.Text);
        var selection = new StagingSelection(WorkingDiffSide.Unstaged, entry, [picked], true);

        var result = await run.ExecuteAsync(
            new DiscardSelectionEffect(Tab, location, selection, false, new DiffCursorMove(0, 0)));

        Assert.IsType<DiscardSucceeded>(result);
        Assert.DoesNotContain(line.Text, await File.ReadAllTextAsync(
            Path.Combine(repository.WorkingTreePath, "file.txt"),
            TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(repository.Git("diff", "--cached", "--name-only").Trim());
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, TestContext.Current.CancellationToken);

    private static ChangedLine Added(int number, string text) => new(DiffLineKind.Added, number, text);

    private static DiscardSelectionEffect WholeFile(StatusEntry entry) =>
        new(Tab, Nowhere, new StagingSelection(WorkingDiffSide.Unstaged, entry, [], false), true, null);

    private static DiscardSelectionEffect Lines(
        IReadOnlyList<ChangedLine> lines,
        bool isRange,
        bool allowWholeFile,
        DiffCursorMove move,
        StatusEntry? entry = null)
    {
        var selection = new StagingSelection(WorkingDiffSide.Unstaged, entry ?? Modified("a.txt"), lines, isRange);
        var whole = allowWholeFile || entry?.Kind == StatusEntryKind.Untracked;

        return new DiscardSelectionEffect(Tab, Nowhere, selection, whole, move);
    }

    // One hunk: "two" becomes "TWO" and a line "four" is added at the end.
    private static DiffFile EditedFile(string path)
    {
        DiffLine[] lines =
        [
            new(DiffLineKind.Context, "one", 1, 1, false),
            new(DiffLineKind.Removed, "two", 2, null, false),
            new(DiffLineKind.Added, "TWO", null, 2, false),
            new(DiffLineKind.Context, "three", 3, 3, false),
            new(DiffLineKind.Added, "four", null, 4, false),
        ];
        var hunk = new DiffHunk("@@ -1,3 +1,5 @@", 1, 3, 1, 5, string.Empty, lines);
        string[] header = [$"diff --git a/{path} b/{path}", $"--- a/{path}", $"+++ b/{path}"];

        return new DiffFile(path, path, DiffFileStatus.Modified, null, null, null, false, header, [hunk]);
    }

    // The handler over a fake discarder and diff reader (or the real ones), a refresher and a dispatcher that record the
    // order in which the status is read and the result is posted.
    private sealed class Run : IStatusRefresher
    {
        private readonly RecordingDispatcher _dispatcher = new();
        private readonly DiscardEffectHandler _handler;

        public Run()
            : this(new FakeDiffReader(), new FakeWorkingTreeDiscarder())
        {
        }

        public Run(IDiffReader reader, IWorkingTreeDiscarder discarder)
        {
            Reader = reader as FakeDiffReader ?? new FakeDiffReader();
            Discarder = discarder as FakeWorkingTreeDiscarder ?? new FakeWorkingTreeDiscarder();
            _handler = new DiscardEffectHandler(new PatchDiscarder(reader, discarder), this, new Ordered(this));
        }

        public FakeWorkingTreeDiscarder Discarder { get; }

        public FakeDiffReader Reader { get; }

        public List<string> Events { get; } = [];

        public void RefreshThen(TabId tabId, Action done)
        {
            Events.Add("refresh");
            done();
        }

        public Task<AppCommand> ExecuteAsync(DiscardEffect effect) =>
            ExecuteWithTokenAsync(effect, TestContext.Current.CancellationToken);

        public async Task<AppCommand> ExecuteWithTokenAsync(DiscardEffect effect, CancellationToken cancellationToken)
        {
            _handler.Handle(effect, cancellationToken);

            return await _dispatcher.NextAsync(TestContext.Current.CancellationToken);
        }

        private sealed class Ordered(Run run) : IDispatcher
        {
            public void Post(AppCommand command)
            {
                run.Events.Add("result");
                run._dispatcher.Post(command);
            }
        }
    }
}
