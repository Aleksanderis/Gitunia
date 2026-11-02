using Gitunia.App.Rebase;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Rebase;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Rebase;

// Real git on temporary repositories: the handler is the only place that runs the rebase commands.
public class RebaseEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenAMergeInTheReplayedRange_WhenCounting_ThenTheMergeIsCounted()
    {
        using var repository = WithMergeOnTopic();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new CountRebaseMergesEffect(Tab, location, TargetOf(repository)));

        Assert.Equal(1, Assert.IsType<RebaseMergesCounted>(result).Merges);
    }

    [Fact]
    public async Task GivenNoMergeInTheRange_WhenCounting_ThenTheCountIsZero()
    {
        using var repository = Diverged();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new CountRebaseMergesEffect(Tab, location, TargetOf(repository)));

        Assert.Equal(0, Assert.IsType<RebaseMergesCounted>(result).Merges);
    }

    [Fact]
    public async Task GivenACommitGitCannotCount_WhenCounting_ThenTheCountIsZeroAndTheConfirmStillCanOpen()
    {
        using var repository = Diverged();
        var location = await LocateAsync(repository);
        var target = new RebaseTarget(Tab, new string('a', 40), "nowhere", "topic", repository.Head);

        var result = await RunAsync(new CountRebaseMergesEffect(Tab, location, target));

        Assert.Equal(0, Assert.IsType<RebaseMergesCounted>(result).Merges);
    }

    [Fact]
    public async Task GivenADivergedBranch_WhenRebasing_ThenItMovesOntoTheTargetAndTheResultCarriesTheNewTip()
    {
        using var repository = Diverged();
        var target = TargetOf(repository);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new RunRebaseEffect(Tab, location, target));

        var finished = Assert.IsType<RebaseFinished>(result);
        Assert.Equal(OperationOutcome.Completed, finished.Result.Outcome);
        Assert.Equal(repository.Head, finished.Result.Head);
        Assert.Equal(target.CommitHash, repository.Git("rev-parse", "HEAD~1").Trim());
    }

    [Fact]
    public async Task GivenAMergeInTheReplayedRange_WhenRebasing_ThenTheRebaseIsLinear()
    {
        using var repository = WithMergeOnTopic();
        var location = await LocateAsync(repository);

        await RunAsync(new RunRebaseEffect(Tab, location, TargetOf(repository)));

        Assert.Equal(string.Empty, repository.Git("rev-list", "--merges", "main..HEAD").Trim());
    }

    [Fact]
    public async Task GivenAConflictingBranch_WhenRebasing_ThenARebaseIsInProgressAndAbortReturnsTheOldTip()
    {
        using var repository = TestRepositories.ConflictingBranches().Checkout("other");
        var oldTip = repository.Head;
        var location = await LocateAsync(repository);

        var result = await RunAsync(new RunRebaseEffect(Tab, location, TargetOf(repository)));

        Assert.Equal(OperationOutcome.Conflicted, Assert.IsType<RebaseFinished>(result).Result.Outcome);
        var detector = new RepositoryOperationDetector();
        Assert.Equal(RepositoryOperation.Rebase, detector.Detect(location));
        var aborted = await new OperationWriter(new OperationRunner(IsolatedGit.CreateRunner(), detector))
            .AbortAsync(location, RepositoryOperation.Rebase, TestContext.Current.CancellationToken);
        Assert.Equal(OperationOutcome.Completed, aborted.Outcome);
        Assert.Equal(oldTip, repository.Head);
    }

    [Fact]
    public async Task GivenACommitThatDoesNotExist_WhenRebasing_ThenGitsReasonComesBackAsTheFailure()
    {
        using var repository = Diverged();
        var location = await LocateAsync(repository);
        var target = new RebaseTarget(Tab, new string('a', 40), "nowhere", "topic", repository.Head);

        var result = await RunAsync(new RunRebaseEffect(Tab, location, target));

        var failed = Assert.IsType<RebaseFailed>(result);
        Assert.StartsWith("Could not rebase topic onto nowhere: ", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMiddleCommit_WhenPlanningADrop_ThenThePlanNamesTheCommitAndTheLaterOnes()
    {
        using var repository = Linear();
        var location = await LocateAsync(repository);
        var hash = repository.Git("rev-parse", "HEAD~1").Trim();

        var result = await RunAsync(new PlanQuickRebaseEffect(Tab, location, QuickRebaseKind.Drop, hash));

        var plan = Assert.IsType<QuickRebasePlanned>(result).Plan;
        Assert.Equal("Three", plan.Subject);
        Assert.Equal(1, plan.LaterCommits);
        Assert.Equal(repository.Head, plan.Head);
    }

    [Fact]
    public async Task GivenTheRootCommit_WhenPlanningAFixup_ThenThePlanFailsWithTheReason()
    {
        using var repository = Linear();
        var location = await LocateAsync(repository);
        var root = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();

        var result = await RunAsync(new PlanQuickRebaseEffect(Tab, location, QuickRebaseKind.Fixup, root));

        var failed = Assert.IsType<QuickRebasePlanFailed>(result);
        Assert.StartsWith("Could not fix up ", failed.Text, StringComparison.Ordinal);
        Assert.Contains("no commit before it", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAPlannedDrop_WhenRunning_ThenTheCommitIsGoneAndTheOldTipComesBack()
    {
        using var repository = Linear();
        var oldTip = repository.Head;
        var location = await LocateAsync(repository);
        var plan = await PlanAsync(location, QuickRebaseKind.Drop, repository.Git("rev-parse", "HEAD~1").Trim());

        var target = new QuickRebaseTarget(Tab, plan, "main", null);

        var result = await RunAsync(new RunQuickRebaseEffect(Tab, location, target));

        var finished = Assert.IsType<QuickRebaseFinished>(result);
        Assert.Equal(oldTip, finished.Result.OriginalHead);
        Assert.Equal(["One", "Two", "Four"], Subjects(repository));
    }

    [Fact]
    public async Task GivenAPlannedFixup_WhenRunning_ThenTheCommitMeldsIntoTheOneBefore()
    {
        using var repository = Linear();
        var location = await LocateAsync(repository);
        var plan = await PlanAsync(location, QuickRebaseKind.Fixup, repository.Git("rev-parse", "HEAD~1").Trim());

        await RunAsync(new RunQuickRebaseEffect(Tab, location, new QuickRebaseTarget(Tab, plan, "main", null)));

        Assert.Equal(["One", "Two", "Four"], Subjects(repository));
    }

    [Fact]
    public async Task GivenAPlannedReword_WhenRunning_ThenTheSubjectChanges()
    {
        using var repository = Linear();
        var location = await LocateAsync(repository);
        var plan = await PlanAsync(location, QuickRebaseKind.Reword, repository.Git("rev-parse", "HEAD~1").Trim());

        await RunAsync(new RunQuickRebaseEffect(Tab, location, new QuickRebaseTarget(Tab, plan, "main", "Third")));

        Assert.Equal(["One", "Two", "Third", "Four"], Subjects(repository));
    }

    [Fact]
    public async Task GivenADropThatConflicts_WhenRunning_ThenARebaseIsInProgress()
    {
        using var repository = TestRepository.Create()
            .Commit("One", new TestFile("f.txt", "1\n"))
            .Commit("Two", new TestFile("f.txt", "1\n2\n"))
            .Commit("Three", new TestFile("f.txt", "1\n2\n3\n"));
        var location = await LocateAsync(repository);
        var plan = await PlanAsync(location, QuickRebaseKind.Drop, repository.Git("rev-parse", "HEAD~1").Trim());

        var target = new QuickRebaseTarget(Tab, plan, "main", null);

        var result = await RunAsync(new RunQuickRebaseEffect(Tab, location, target));

        Assert.Equal(OperationOutcome.Conflicted, Assert.IsType<QuickRebaseFinished>(result).Result.Outcome);
        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenRebasing_ThenNothingChangesAndTheResultIsACancel()
    {
        using var repository = Diverged();
        var old = repository.Head;
        var location = await LocateAsync(repository);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await RunWithTokenAsync(new RunRebaseEffect(Tab, location, TargetOf(repository)), cancelled.Token);

        Assert.IsType<RebaseCancelled>(result);
        Assert.Equal(old, repository.Head);
    }

    // Topic is checked out and main has moved on; the target is main.
    private static TestRepository Diverged() =>
        TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("Topic change", new TestFile("topic.txt", "topic\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("main.txt", "main\n"))
            .Checkout("topic");

    // Topic has a side branch merged into it, and main has moved on.
    private static TestRepository WithMergeOnTopic() =>
        TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("Topic change", new TestFile("topic.txt", "topic\n"))
            .CheckoutNewBranch("side")
            .Commit("Side change", new TestFile("side.txt", "side\n"))
            .Checkout("topic")
            .Commit("Topic again", new TestFile("topic2.txt", "topic2\n"))
            .Merge("side")
            .Checkout("main")
            .Commit("Main change", new TestFile("main.txt", "main\n"))
            .Checkout("topic");

    private static TestRepository Linear() =>
        TestRepository.Create()
            .Commit("One", new TestFile("one.txt", "1\n"))
            .Commit("Two", new TestFile("two.txt", "2\n"))
            .Commit("Three", new TestFile("three.txt", "3\n"))
            .Commit("Four", new TestFile("four.txt", "4\n"));

    private static string[] Subjects(TestRepository repository) =>
        repository.Git("log", "--reverse", "--format=%s").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static RebaseTarget TargetOf(TestRepository repository, string onto = "main")
    {
        var branch = repository.Git("branch", "--show-current").Trim();

        return new RebaseTarget(Tab, repository.Git("rev-parse", onto).Trim(), onto, branch, repository.Head);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static async Task<QuickRebasePlan> PlanAsync(RepositoryLocation location, QuickRebaseKind kind, string hash)
    {
        var result = await RunAsync(new PlanQuickRebaseEffect(Tab, location, kind, hash));

        return Assert.IsType<QuickRebasePlanned>(result).Plan;
    }

    private static Task<AppCommand> RunAsync(RebaseEffect effect) =>
        RunWithTokenAsync(effect, TestContext.Current.CancellationToken);

    // The quick rebase writes its todo through a copy command instead of the app binary, which tests do not have.
    private static async Task<AppCommand> RunWithTokenAsync(RebaseEffect effect, CancellationToken cancellationToken)
    {
        var runner = IsolatedGit.CreateRunner();
        var detector = new RepositoryOperationDetector();
        var interactive = new InteractiveRebaseWriter(
            new OperationRunner(runner, detector),
            runner,
            detector,
            new SequenceEditorCommand(["cp"]),
            _ => null);
        var dispatcher = new RecordingDispatcher();
        new RebaseEffectHandler(TestGit.Services.Rebase, new QuickRebaseWriter(runner, interactive), dispatcher)
            .Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
