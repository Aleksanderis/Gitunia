using Gitunia.App.Rebase;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Rebase;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Rebase;

// Real git on temporary repositories: the handler is the only place that reads the range and runs the todo.
public class RebaseEditorEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenABranchAheadOfTheTarget_WhenReadingTheRange_ThenTheCommitsAreListedOldestFirstAsPicks()
    {
        using var repository = ThreeCommits();
        var location = await LocateAsync(repository);

        var result = await RunAsync(RangeOf(location, repository.Git("rev-parse", "main").Trim(), repository.Head));

        var read = Assert.IsType<RebaseEditorRangeRead>(result);
        Assert.Equal(["add a", "add b", "add c"], read.State.Rows.Select(row => row.Subject));
        Assert.All(read.State.Rows, row => Assert.Equal(RebaseTodoAction.Pick, row.Action));
        Assert.Equal("topic", read.State.BranchName);
        Assert.Equal(repository.Head, read.State.OldTip);
        Assert.Equal(7, read.Generation);
        Assert.Equal(Tab, read.State.TabId);
    }

    [Fact]
    public async Task GivenNoTarget_WhenReadingTheRange_ThenTheWholeHistoryIsListedFromTheRoot()
    {
        using var repository = ThreeCommits();
        var location = await LocateAsync(repository);

        var result = await RunAsync(RangeOf(location, null, repository.Head));

        var read = Assert.IsType<RebaseEditorRangeRead>(result);
        Assert.True(read.State.Base.IsRoot);
        Assert.Equal(["Initial", "add a", "add b", "add c"], read.State.Rows.Select(row => row.Subject));
    }

    [Fact]
    public async Task GivenARangeWithAMergeCommit_WhenReading_ThenTheMergeAndItsStructureLinesAreListed()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("topic one", new TestFile("one.txt", "1\n"))
            .Checkout("main")
            .CheckoutNewBranch("side")
            .Commit("side", new TestFile("side.txt", "s\n"))
            .Checkout("topic")
            .Merge("side");
        var location = await LocateAsync(repository);

        var result = await RunAsync(RangeOf(location, repository.Git("rev-parse", "main").Trim(), repository.Head));

        var rows = Assert.IsType<RebaseEditorRangeRead>(result).State.Rows;
        Assert.Contains(
            rows,
            row => row.Action == RebaseTodoAction.Merge && row.Subject.StartsWith("Merge", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Action == RebaseTodoAction.Label);
    }

    [Fact]
    public async Task GivenATargetTheBranchAlreadyHas_WhenReading_ThenTheRefusalIsAWarningAndNothingOpens()
    {
        using var repository = ThreeCommits();
        var location = await LocateAsync(repository);

        var result = await RunAsync(RangeOf(location, repository.Head, repository.Head));

        var failed = Assert.IsType<RebaseEditorReadFailed>(result);
        Assert.Equal(StatusSeverity.Warning, failed.Severity);
        Assert.Contains("no commits to rebase", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACommitGitDoesNotKnow_WhenReadingTheRange_ThenTheFailureIsReportedAndNothingOpens()
    {
        using var repository = ThreeCommits();
        var location = await LocateAsync(repository);

        var result = await RunAsync(RangeOf(location, new string('a', 40), repository.Head));

        var failed = Assert.IsType<RebaseEditorReadFailed>(result);
        Assert.NotEmpty(failed.Text);
        Assert.Equal(StatusSeverity.Error, failed.Severity);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReadingTheRange_ThenTheResultIsACancel()
    {
        using var repository = ThreeCommits();
        var location = await LocateAsync(repository);

        var result = await RunCancelledAsync(RangeOf(location, null, repository.Head));

        Assert.IsType<RebaseEditorReadCancelled>(result);
    }

    [Fact]
    public async Task GivenACommitWithABody_WhenReadingItsMessage_ThenTheWholeMessageComesBack()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .Commit("subject line\n\nthe body\nof it", new TestFile("a.txt", "a\n"));
        var location = await LocateAsync(repository);
        var hash = repository.Head;

        var result = await RunAsync(new ReadRebaseMessageEffect(Tab, location, 3, hash, Parse(hash), "subject line"));

        var read = Assert.IsType<RebaseEditorMessageRead>(result);
        Assert.Equal(hash, read.Hash);
        Assert.Equal(3, read.Generation);
        Assert.StartsWith("subject line\n\nthe body\nof it", read.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACommitGitCannotFind_WhenReadingItsMessage_ThenTheSubjectIsTheStart()
    {
        using var repository = ThreeCommits();
        var location = await LocateAsync(repository);
        var missing = new string('b', 40);

        var result = await RunAsync(new ReadRebaseMessageEffect(Tab, location, 1, missing, Parse(missing), "the subject"));

        Assert.Equal("the subject", Assert.IsType<RebaseEditorMessageRead>(result).Text);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReadingAMessage_ThenTheResultIsACancel()
    {
        using var repository = ThreeCommits();
        var location = await LocateAsync(repository);

        var result = await RunCancelledAsync(
            new ReadRebaseMessageEffect(Tab, location, 1, repository.Head, Parse(repository.Head), "s"));

        Assert.IsType<RebaseEditorReadCancelled>(result);
    }

    [Fact]
    public async Task GivenAReorderedFixedUpAndRewordedTodo_WhenRunning_ThenGitReplaysItAndTheResultCarriesTheTips()
    {
        using var repository = ThreeCommits().Commit("add d", new TestFile("d.txt", "d\n"));
        var oldTip = repository.Head;
        var location = await LocateAsync(repository);
        var read = await ReadAsync(location, repository);

        // Rows: a, b, c, d. Move c above b, fix d up into b, reword a.
        var state = read.State.MoveTo(2).MoveRow(-1).MoveTo(3).SetAction(RebaseTodoAction.Fixup);
        state = state.MoveTo(0).BeginEdit("a reworded\n\nwith a body").SaveEdit();

        var result = await RunAsync(new RunInteractiveRebaseEffect(Tab, location, state));

        var finished = Assert.IsType<RebaseEditorFinished>(result);
        Assert.Equal(OperationOutcome.Completed, finished.Result.Outcome);
        Assert.Equal(oldTip, finished.Result.OriginalHead);
        Assert.Equal(repository.Head, finished.Result.Head);
        Assert.Equal(
            ["add b", "add c", "a reworded"],
            repository.Git("log", "--format=%s", "main..HEAD").Trim().Split('\n'));
        Assert.Equal("with a body", repository.Git("log", "-1", "--format=%b", "HEAD~2").Trim());
        Assert.Equal("d\n", repository.Git("show", "HEAD:d.txt").Replace("\r", string.Empty));
    }

    [Fact]
    public async Task GivenADroppedCommit_WhenRunning_ThenTheCommitIsGone()
    {
        using var repository = ThreeCommits();
        var location = await LocateAsync(repository);
        var read = await ReadAsync(location, repository);
        var state = read.State.MoveTo(1).SetAction(RebaseTodoAction.Drop);

        await RunAsync(new RunInteractiveRebaseEffect(Tab, location, state));

        Assert.Equal(
            ["add c", "add a"],
            repository.Git("log", "--format=%s", "main..HEAD").Trim().Split('\n'));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
    }

    [Fact]
    public async Task GivenAConflictingReorder_WhenRunning_ThenARebaseIsInProgressAndTheResultIsConflicted()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("f.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("first", new TestFile("f.txt", "first\n"))
            .Commit("second", new TestFile("f.txt", "second\n"));
        var location = await LocateAsync(repository);
        var read = await ReadAsync(location, repository);
        var state = read.State.MoveTo(1).MoveRow(-1);

        var result = await RunAsync(new RunInteractiveRebaseEffect(Tab, location, state));

        var finished = Assert.IsType<RebaseEditorFinished>(result);
        Assert.Equal(OperationOutcome.Conflicted, finished.Result.Outcome);
        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenGitSequenceEditorIsSet_WhenRunning_ThenNothingRunsAndTheTodoComesBackWithTheReason()
    {
        using var repository = ThreeCommits();
        var oldTip = repository.Head;
        var location = await LocateAsync(repository);
        var read = await ReadAsync(location, repository);
        var state = read.State.SetAction(RebaseTodoAction.Drop);

        var result = await RunWithEnvironmentAsync(
            new RunInteractiveRebaseEffect(Tab, location, state),
            name => name == "GIT_SEQUENCE_EDITOR" ? "vim" : null);

        var notStarted = Assert.IsType<RebaseEditorNotStarted>(result);
        Assert.Same(state, notStarted.State);
        Assert.Contains("GIT_SEQUENCE_EDITOR", notStarted.Text, StringComparison.Ordinal);
        Assert.Equal(oldTip, repository.Head);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenRunning_ThenNothingChangesAndTheResultIsACancel()
    {
        using var repository = ThreeCommits();
        var oldTip = repository.Head;
        var location = await LocateAsync(repository);
        var read = await ReadAsync(location, repository);

        var result = await RunCancelledAsync(new RunInteractiveRebaseEffect(Tab, location, read.State));

        Assert.IsType<RebaseCancelled>(result);
        Assert.Equal(oldTip, repository.Head);
    }

    // The range is read through the handler too, as the editor does.
    private static async Task<RebaseEditorRangeRead> ReadAsync(RepositoryLocation location, TestRepository repository)
    {
        var target = repository.Git("rev-parse", "main").Trim();

        return Assert.IsType<RebaseEditorRangeRead>(await RunAsync(RangeOf(location, target, repository.Head)));
    }

    private static ReadRebaseRangeEffect RangeOf(RepositoryLocation location, string? target, string oldTip) =>
        new(Tab, location, 7, target, target?[..7] ?? "the root", "topic", oldTip);

    private static ObjectId Parse(string hash)
    {
        Assert.True(ObjectId.TryParse(hash, out var id));

        return id;
    }

    // main has "Initial"; topic adds a.txt, b.txt, c.txt in three commits.
    private static TestRepository ThreeCommits() =>
        TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("add a", new TestFile("a.txt", "a\n"))
            .Commit("add b", new TestFile("b.txt", "b\n"))
            .Commit("add c", new TestFile("c.txt", "c\n"));

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static Task<AppCommand> RunAsync(RebaseEditorEffect effect) =>
        ExecuteAsync(effect, _ => null, false);

    private static Task<AppCommand> RunCancelledAsync(RebaseEditorEffect effect) =>
        ExecuteAsync(effect, _ => null, true);

    private static Task<AppCommand> RunWithEnvironmentAsync(
        RebaseEditorEffect effect,
        Func<string, string?> environment) => ExecuteAsync(effect, environment, false);

    // The engine writes its todo through a copy command instead of the app binary, which tests do not have.
    private static async Task<AppCommand> ExecuteAsync(
        RebaseEditorEffect effect,
        Func<string, string?> environment,
        bool isCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        if (isCancelled)
        {
            await cancellation.CancelAsync();
        }

        var runner = IsolatedGit.CreateRunner();
        var detector = new RepositoryOperationDetector();
        var interactive = new InteractiveRebaseWriter(
            new OperationRunner(runner, detector),
            runner,
            detector,
            new SequenceEditorCommand(["cp"]),
            environment);
        var dispatcher = new RecordingDispatcher();
        new RebaseEditorEffectHandler(interactive, new CommitDetailsReader(runner), dispatcher)
            .Handle(effect, cancellation.Token);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
