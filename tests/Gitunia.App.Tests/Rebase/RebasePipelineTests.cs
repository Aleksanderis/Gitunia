using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Status;
using Gitunia.Git.Operations;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Rebase;

// The rebase actions end to end: command -> reducer -> effect -> runner -> handler -> real git -> dialog -> result.
public class RebasePipelineTests
{
    [Fact]
    public async Task GivenADivergedBranch_WhenRebasingAfterConfirming_ThenItMovesOntoTheTargetAndNamesTheUndo()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        var mainTip = repository.Git("rev-parse", "main").Trim();
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(Onto(pipeline, mainTip));
        await pipeline.UntilAsync(() => pipeline.Confirm is not null);

        var prompt = pipeline.Confirm!.Prompt;
        Assert.Equal("Rebase", prompt.Title);
        Assert.Contains($"Rebase topic onto {mainTip[..7]}?", prompt.Lines, StringComparer.Ordinal);
        Assert.Equal(oldTip, repository.Head);
        Assert.False(pipeline.IsBusy);

        pipeline.Answer(ConfirmAnswer.Yes);
        Assert.True(pipeline.IsBusy);
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.Equal(mainTip, repository.Git("rev-parse", "HEAD~1").Trim());
        var latest = pipeline.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Info, latest.Severity);
        Assert.Contains($"was {oldTip[..7]}", latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMergeInTheReplayedRange_WhenAsking_ThenTheConfirmWarnsAndTheRebaseIsLinear()
    {
        using var repository = WithMergeOnTopic();
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(Onto(pipeline, repository.Git("rev-parse", "main").Trim()));
        await pipeline.UntilAsync(() => pipeline.Confirm is not null);

        Assert.Contains(
            pipeline.Confirm!.Prompt.Lines,
            line => line.StartsWith("1 merge commit in that range will be flattened", StringComparison.Ordinal));
        pipeline.Answer(ConfirmAnswer.Yes);
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.Equal(string.Empty, repository.Git("rev-list", "--merges", "main..HEAD").Trim());
    }

    [Fact]
    public async Task GivenAConflictingBranch_WhenRebasing_ThenTheWarningPointsToContinueAndARebaseIsInProgress()
    {
        using var repository = TestRepositories.ConflictingBranches().Checkout("other");
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(Onto(pipeline, repository.Git("rev-parse", "main").Trim()));
        await pipeline.UntilAsync(() => pipeline.Confirm is not null);

        pipeline.Answer(ConfirmAnswer.Yes);
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        var latest = pipeline.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("stopped on conflicts", latest.Text, StringComparison.Ordinal);
        Assert.Contains("<space>gc", latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACommit_WhenRewordedWithANewSubject_ThenThePromptThenTheConfirmThenTheMessageChanges()
    {
        using var repository = Linear();
        using var pipeline = await FeaturePipeline.OpenAsync(QuickServices(), repository);

        pipeline.Dispatch(new QuickRebaseRequested(
            pipeline.TabId,
            QuickRebaseKind.Reword,
            repository.Git("rev-parse", "HEAD~1").Trim()));
        await pipeline.UntilAsync(() => pipeline.Prompt is not null);

        Assert.Equal("Reword commit", pipeline.Prompt!.Title);
        Assert.Equal("Three", pipeline.Prompt.Text);
        Assert.Null(pipeline.Confirm);
        Assert.False(pipeline.IsBusy);
        pipeline.Type("d");
        pipeline.Dispatch(new PromptAcceptedCommand());
        Assert.Equal("Reword commit", pipeline.Confirm!.Prompt.Title);
        Assert.Contains("to \"Threed\"", pipeline.Confirm.Prompt.Lines[0], StringComparison.Ordinal);
        pipeline.Answer(ConfirmAnswer.Yes);
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.Equal(["One", "Two", "Threed", "Four"], Subjects(repository));
    }

    [Fact]
    public async Task GivenTheRootCommit_WhenFixingUp_ThenTheReasonIsShownAndNothingIsAsked()
    {
        using var repository = Linear();
        using var pipeline = await FeaturePipeline.OpenAsync(QuickServices(), repository);

        pipeline.Dispatch(new QuickRebaseRequested(
            pipeline.TabId,
            QuickRebaseKind.Fixup,
            repository.Git("rev-list", "--max-parents=0", "HEAD").Trim()));
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
        Assert.Contains("no commit before it", pipeline.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Null(pipeline.Confirm);
    }

    [Fact]
    public async Task GivenTrackedChanges_WhenDropping_ThenItIsRefusedBeforeAnyQuestion()
    {
        using var repository = Linear();
        File.WriteAllText(Path.Combine(repository.WorkingTreePath, "one.txt"), "changed\n");
        using var pipeline = await FeaturePipeline.OpenAsync(QuickServices(), repository);
        await pipeline.UntilAsync(() => pipeline.Host.Workspace.FindTab(pipeline.TabId)!.WorkingTree is not null);

        pipeline.Dispatch(new QuickRebaseRequested(pipeline.TabId, QuickRebaseKind.Drop, repository.Head));

        Assert.Contains("uncommitted changes", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Null(pipeline.Confirm);
        Assert.False(pipeline.IsBusy);
    }

    private static RebaseOntoRequested Onto(FeaturePipeline pipeline, string commit) =>
        new(pipeline.TabId, new RebaseSelection(null, null, commit));

    // The quick rebase writes its todo through a copy command instead of the app binary, which tests do not have.
    private static GitServices QuickServices()
    {
        var runner = IsolatedGit.CreateRunner();
        var detector = new RepositoryOperationDetector();
        var interactive = new InteractiveRebaseWriter(
            new OperationRunner(runner, detector),
            runner,
            detector,
            new SequenceEditorCommand(["cp"]),
            _ => null);

        return TestGit.Services with { QuickRebase = new QuickRebaseWriter(runner, interactive) };
    }

    private static string[] Subjects(TestRepository repository) =>
        repository.Git("log", "--reverse", "--format=%s").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static TestRepository Linear() =>
        TestRepository.Create()
            .Commit("One", new TestFile("one.txt", "1\n"))
            .Commit("Two", new TestFile("two.txt", "2\n"))
            .Commit("Three", new TestFile("three.txt", "3\n"))
            .Commit("Four", new TestFile("four.txt", "4\n"));

    private static TestRepository Diverged() =>
        TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("Topic change", new TestFile("topic.txt", "topic\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("main.txt", "main\n"))
            .Checkout("topic");

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
}
