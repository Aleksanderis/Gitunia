using Gitunia.App.Tests.Tags;
using Gitunia.App.Staging;
using Gitunia.Core;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Diff;
using Gitunia.Git.Staging;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.App.Tests.Staging;

// The handler is the only place that changes the index. Fake writers show what git is asked; real git shows it works.
public class StagingEffectHandlerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly RepositoryLocation Nowhere = new("/r", "/r/.git", "/r/.git", false, false);

    [Fact]
    public async Task GivenPaths_WhenStaging_ThenTheWriterGetsThemAndTheResultNamesThemAfterTheStatusWasRead()
    {
        var run = new Run();
        var plan = StagingPlan.For(["b.txt"], "b.txt", 1, 0);

        var result = await run.ExecuteAsync(new StagePathsEffect(Tab, Nowhere, plan, true, false, StagingFollow.None));

        var success = Assert.IsType<StagingSucceeded>(result);
        Assert.Equal("Staged b.txt", success.Text);
        Assert.Equal(StatusSeverity.Info, success.Severity);
        Assert.Equal(["b.txt"], Assert.Single(run.Writer.Staged));
        Assert.Equal(["refresh", "result"], run.Events);
    }

    [Fact]
    public async Task GivenAStagedRenameAndAnUnbornBranch_WhenUnstaging_ThenBothPathsAndTheFlagReachTheWriter()
    {
        var run = new Run();
        var plan = StagingPlan.For(["new.txt", "old.txt"], "new.txt", 1, 0);

        var result = await run.ExecuteAsync(new StagePathsEffect(Tab, Nowhere, plan, false, true, StagingFollow.None));

        Assert.Equal("Unstaged new.txt", Assert.IsType<StagingSucceeded>(result).Text);
        Assert.Equal(["new.txt", "old.txt"], Assert.Single(run.Writer.Unstaged));
        Assert.Equal([true], run.Writer.UnbornFlags);
    }

    [Fact]
    public async Task GivenTheFollowOfTheRequest_WhenItSucceeds_ThenTheResultReturnsItUnchanged()
    {
        var run = new Run();
        var follow = new StagingFollow(Resolved: new ResolvedFile("c.txt", "c.txt"));
        var plan = StagingPlan.For(["c.txt"], "c.txt", 1, 0) with { IsSavedResolve = true };

        var result = await run.ExecuteAsync(new StagePathsEffect(Tab, Nowhere, plan, true, false, follow));

        var success = Assert.IsType<StagingSucceeded>(result);
        Assert.Equal("Resolved and staged c.txt", success.Text);
        Assert.Same(follow, success.Follow);
    }

    [Fact]
    public async Task GivenAConflictedFileMarkedResolved_WhenItSucceeds_ThenTheHistoryDetailHasTheRecreateCommand()
    {
        var run = new Run();
        var plan = StagingPlan.For(["c.txt"], "c.txt", 1, 0) with { IsResolve = true };

        var result = await run.ExecuteAsync(new StagePathsEffect(Tab, Nowhere, plan, true, false, StagingFollow.None));

        var success = Assert.IsType<StagingSucceeded>(result);
        Assert.DoesNotContain("git ", success.Text, StringComparison.Ordinal);
        Assert.StartsWith("Undo: git checkout --merge -- c.txt", success.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenGitFails_WhenStaging_ThenTheErrorIsTheResultAndTheStatusIsStillReadAgain()
    {
        var run = new Run();
        run.Writer.Failure = new InvalidOperationException("index.lock exists");
        var plan = StagingPlan.For(["a.txt"], "a.txt", 1, 0);

        var result = await run.ExecuteAsync(new StagePathsEffect(Tab, Nowhere, plan, true, false, StagingFollow.None));

        var failed = Assert.IsType<StagingFailed>(result);
        Assert.StartsWith("Could not stage: ", failed.Text, StringComparison.Ordinal);
        Assert.Contains("index.lock exists", failed.Text, StringComparison.Ordinal);
        Assert.Equal(["refresh", "result"], run.Events);
    }

    [Fact]
    public async Task GivenACancelledTab_WhenStaging_ThenTheResultIsACancelAndNothingIsRead()
    {
        var run = new Run();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        run.Writer.Failure = new OperationCanceledException(source.Token);
        var plan = StagingPlan.For(["a.txt"], "a.txt", 1, 0);

        var result = await run.ExecuteWithTokenAsync(
            new StagePathsEffect(Tab, Nowhere, plan, true, false, StagingFollow.None),
            source.Token);

        Assert.IsType<StagingCancelled>(result);
        Assert.Equal(["result"], run.Events);
    }

    [Fact]
    public async Task GivenChosenLines_WhenStagingTheSelection_ThenAForwardPatchOfJustThoseLinesIsApplied()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));
        var move = new DiffCursorMove(0, 1);

        var result = await run.ExecuteAsync(Selection(WorkingDiffSide.Unstaged, [Added(2, "TWO")], true, move));

        var success = Assert.IsType<StagingSucceeded>(result);
        Assert.Equal("Staged 1 line of a.txt", success.Text);
        Assert.Equal(move, success.Follow.Move);
        var patch = Assert.Single(run.Writer.Patches);
        Assert.Equal(PatchDirection.Forward, patch.Direction);
        Assert.Equal(
            "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,3 +1,4 @@\n one\n two\n+TWO\n three\n",
            patch.Text);
    }

    [Fact]
    public async Task GivenAStagedFile_WhenUnstagingTheHunk_ThenAReversePatchIsApplied()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Staged, "a.txt", EditedFile("a.txt"));

        var result = await run.ExecuteAsync(Selection(WorkingDiffSide.Staged, [Added(4, "four")], false));

        Assert.Equal("Unstaged change (1 line) of a.txt", Assert.IsType<StagingSucceeded>(result).Text);
        Assert.Equal(PatchDirection.Reverse, Assert.Single(run.Writer.Patches).Direction);
    }

    [Fact]
    public async Task GivenALineThatIsNoLongerInTheFile_WhenStagingTheSelection_ThenNothingIsAppliedAndTheCursorStays()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));

        var result = await run.ExecuteAsync(
            Selection(WorkingDiffSide.Unstaged, [Added(2, "something else")], true, new DiffCursorMove(0, 1)));

        var success = Assert.IsType<StagingSucceeded>(result);
        Assert.Empty(run.Writer.Patches);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
        Assert.Contains("changed since the diff was read", success.Text, StringComparison.Ordinal);
        Assert.Equal(StagingFollow.None, success.Follow);
    }

    [Fact]
    public async Task GivenGitRefusesThePatch_WhenStagingTheSelection_ThenTheErrorIsTheResultAndTheStatusIsReadAgain()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));
        run.Writer.Failure = new InvalidOperationException("patch does not apply");

        var result = await run.ExecuteAsync(Selection(WorkingDiffSide.Unstaged, [Added(2, "TWO")], true));

        var failed = Assert.IsType<StagingFailed>(result);
        Assert.StartsWith("Could not stage the selection: ", failed.Text, StringComparison.Ordinal);
        Assert.Contains("patch does not apply", failed.Text, StringComparison.Ordinal);
        Assert.Equal(["refresh", "result"], run.Events);
    }

    [Fact]
    public async Task GivenADeletedFileWithoutHunks_WhenStagingTheHunk_ThenTheWholeFileIsStagedAndSaysSo()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Unstaged, "new.txt");

        var result = await run.ExecuteAsync(Selection(WorkingDiffSide.Unstaged, [Added(1, "x")], false, entry: Deleted("new.txt")));

        var success = Assert.IsType<StagingSucceeded>(result);
        Assert.Equal(["new.txt"], Assert.Single(run.Writer.Staged));
        Assert.Empty(run.Writer.Patches);
        Assert.Equal("Staged new.txt whole: new, deleted and binary files are not split into hunks", success.Text);
        Assert.NotNull(success.Follow.Move);
    }

    [Fact]
    public async Task GivenLinesPickedInAFileThatCannotBeSplit_WhenStaging_ThenNothingIsStagedAndAWarningShows()
    {
        var run = new Run();
        run.Reader.SetWorking(WorkingDiffSide.Unstaged, "new.txt");

        var result = await run.ExecuteAsync(
            Selection(WorkingDiffSide.Unstaged, [Added(1, "x")], true, new DiffCursorMove(0, 0), Deleted("new.txt")));

        var success = Assert.IsType<StagingSucceeded>(result);
        Assert.Empty(run.Writer.Staged);
        Assert.Empty(run.Writer.Patches);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
        Assert.Contains("cannot be split into lines", success.Text, StringComparison.Ordinal);
        Assert.Equal(StagingFollow.None, success.Follow);
    }

    [Fact]
    public async Task GivenARealRepository_WhenStagingAndUnstagingAPath_ThenTheIndexFollows()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        var location = await LocateAsync(repository);
        var plan = StagingPlan.For(["file.txt"], "file.txt", 1, 0);
        var run = new Run(TestGit.Services.IndexWriter, TestGit.Services.Diff);

        await run.ExecuteAsync(new StagePathsEffect(Tab, location, plan, true, false, StagingFollow.None));
        Assert.Equal("file.txt", repository.Git("diff", "--cached", "--name-only").Trim());

        await run.ExecuteAsync(new StagePathsEffect(Tab, location, plan, false, false, StagingFollow.None));
        Assert.Empty(repository.Git("diff", "--cached", "--name-only").Trim());
    }

    [Fact]
    public async Task GivenARealRepository_WhenStagingOneLine_ThenOnlyThatLineIsInTheIndex()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "one\nchanged\nnext\n");
        var location = await LocateAsync(repository);
        var run = new Run(TestGit.Services.IndexWriter, TestGit.Services.Diff);
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
        var effect = new StageSelectionEffect(
            Tab,
            location,
            new StagingSelection(WorkingDiffSide.Unstaged, entry, [picked], true),
            false,
            new DiffCursorMove(0, 0));

        var result = await run.ExecuteAsync(effect);

        Assert.IsType<StagingSucceeded>(result);
        Assert.Contains($"+{line.Text}", repository.Git("diff", "--cached"), StringComparison.Ordinal);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, TestContext.Current.CancellationToken);

    private static ChangedLine Added(int number, string text) => new(DiffLineKind.Added, number, text);

    private static StageSelectionEffect Selection(
        WorkingDiffSide side,
        IReadOnlyList<ChangedLine> lines,
        bool isRange,
        DiffCursorMove? move = null,
        StatusEntry? entry = null) =>
        new(
            Tab,
            Nowhere,
            new StagingSelection(side, entry ?? Modified("a.txt"), lines, isRange),
            false,
            move ?? new DiffCursorMove(0, -1));

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

    // The handler over a fake index writer and diff reader (or the real ones), a refresher and a dispatcher that
    // record the order in which the status is read and the result is posted.
    private sealed class Run : IStatusRefresher
    {
        private readonly IIndexWriter _writer;
        private readonly IDiffReader _reader;
        private readonly RecordingDispatcher _dispatcher = new();
        private readonly StagingEffectHandler _handler;

        public Run()
            : this(new FakeIndexWriter(), new FakeDiffReader())
        {
        }

        public Run(IIndexWriter writer, IDiffReader reader)
        {
            _writer = writer;
            _reader = reader;
            _handler = new StagingEffectHandler(writer, new PatchStager(reader, writer), this, new Ordered(this));
        }

        public FakeIndexWriter Writer => (FakeIndexWriter)_writer;

        public FakeDiffReader Reader => (FakeDiffReader)_reader;

        public List<string> Events { get; } = [];

        public void RefreshThen(TabId tabId, Action done)
        {
            Events.Add("refresh");
            done();
        }

        public Task<AppCommand> ExecuteAsync(StagingEffect effect) =>
            ExecuteWithTokenAsync(effect, TestContext.Current.CancellationToken);

        public async Task<AppCommand> ExecuteWithTokenAsync(StagingEffect effect, CancellationToken cancellationToken)
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
