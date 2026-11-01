using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Integration;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Merge;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Integration;

// Reset, merge and the operation in progress end to end: command -> reducer -> effect -> runner -> handler -> real
// git -> result command.
public class IntegrationPipelineTests
{
    [Fact]
    public async Task GivenADirtyTree_WhenResettingHardThroughThePromptAndTheDialog_ThenOnlyYMovesTheBranch()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "dirty");
        var old = repository.Head;
        var first = FirstCommit(repository);
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new ResetRequested(pipeline.TabId, first));
        Assert.Equal("Reset", pipeline.Host.State.Shell.Prompt!.Title);
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        pipeline.Dispatch(new PromptKeyPressedCommand(new KeyEvent("h")));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        Assert.Equal("Hard reset", pipeline.Host.State.Shell.Confirm!.Prompt.Title);
        Assert.Contains("  a.txt", pipeline.Host.State.Shell.Confirm.Prompt.Lines);
        Assert.Equal(old, repository.Head);
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);

        Assert.Equal(first, repository.Head);
        Assert.Equal("1", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
        Assert.Contains($"git reset --hard {old}", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenTheHardResetDialog_WhenAnsweringNo_ThenNothingMoves()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        using var pipeline = await Pipeline.OpenAsync(repository);
        pipeline.Dispatch(new ResetRequested(pipeline.TabId, FirstCommit(repository)));
        pipeline.Dispatch(new PromptKeyPressedCommand(new KeyEvent("h")));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.False(pipeline.IsRunning);
        Assert.Equal(old, repository.Head);
    }

    [Fact]
    public async Task GivenTheHardResetDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingIsReset()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "dirty");
        var old = repository.Head;
        using var pipeline = await Pipeline.OpenAsync(repository);
        var tabId = pipeline.TabId;
        pipeline.Dispatch(new ResetRequested(tabId, FirstCommit(repository)));
        pipeline.Dispatch(new PromptKeyPressedCommand(new KeyEvent("h")));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        pipeline.Dispatch(new CloseTabCommand(tabId));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(old, repository.Head);
        Assert.Equal("dirty", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenACommit_WhenResettingSoft_ThenItRunsWithoutADialogAndTheTabIsFreeAgain()
    {
        using var repository = TwoCommits();
        var first = FirstCommit(repository);
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new ResetRequested(pipeline.TabId, first));
        pipeline.Dispatch(new PromptKeyPressedCommand(new KeyEvent("s")));
        Assert.True(pipeline.IsRunning);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(first, repository.Head);
        Assert.StartsWith("Reset main to", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMergeInProgress_WhenResetting_ThenItIsRefusedWithoutAPromptAndTheMergeStays()
    {
        using var repository = TestRepositories.Conflicted();
        var old = repository.Head;
        using var pipeline = await Pipeline.OpenAsync(repository);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Tab.Operation is not null);

        pipeline.Dispatch(new ResetRequested(pipeline.TabId, FirstCommit(repository)));

        Assert.Null(pipeline.Host.State.Shell.Prompt);
        Assert.Equal(
            "A merge is in progress: finish or abort the merge first",
            pipeline.Statuses.Log.Latest!.Text);
        Assert.Equal(old, repository.Head);
    }

    [Fact]
    public async Task GivenABranchAhead_WhenMerging_ThenItFastForwardsAndTheTabReloads()
    {
        using var repository = AheadBranch();
        var topic = repository.Git("rev-parse", "topic").Trim();
        using var pipeline = await Pipeline.OpenAsync(repository);

        var target = new MergeTarget("refs/heads/topic", "topic");

        pipeline.Dispatch(new MergeRequested(pipeline.TabId, MergeMode.Default, target));
        Assert.True(pipeline.IsRunning);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Tab.Head?.CommitHash == topic);

        Assert.Equal(topic, repository.Head);
        Assert.StartsWith("Fast-forwarded main", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMergeStoppedOnConflicts_WhenAbortingAndAnsweringYes_ThenTheOldTipReturnsAndTheBannerGoes()
    {
        using var repository = TestRepositories.Conflicted();
        var before = repository.Head;
        using var pipeline = await Pipeline.OpenAsync(repository);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Tab.Operation is not null);

        pipeline.Dispatch(new OperationRequested(pipeline.TabId, OperationAction.Abort));
        Assert.Equal("Abort merge", pipeline.Host.State.Shell.Confirm!.Prompt.Title);
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Tab.Operation is null);

        Assert.Equal(before, repository.Head);
        Assert.StartsWith("Aborted the merge", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnAbortDialog_WhenTheTabIsClosedAndYIsAnswered_ThenTheOperationStays()
    {
        using var repository = TestRepositories.Conflicted();
        using var pipeline = await Pipeline.OpenAsync(repository);
        var tabId = pipeline.TabId;
        await pipeline.Pump.RunUntilAsync(() => pipeline.Tab.Operation is not null);
        pipeline.Dispatch(new OperationRequested(tabId, OperationAction.Abort));
        Assert.NotNull(pipeline.Host.State.Shell.Confirm);

        pipeline.Dispatch(new CloseTabCommand(tabId));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, ".git", "MERGE_HEAD")));
    }

    [Fact]
    public async Task GivenAMergeStoppedOnConflicts_WhenContinuing_ThenItIsRefusedAndCountsTheFiles()
    {
        using var repository = TestRepositories.Conflicted();
        using var pipeline = await Pipeline.OpenAsync(repository);
        await pipeline.Pump.RunUntilAsync(
            () => pipeline.Tab.Operation is not null && pipeline.Tab.WorkingTree is not null);

        pipeline.Dispatch(new OperationRequested(pipeline.TabId, OperationAction.Continue));

        Assert.False(pipeline.IsRunning);
        Assert.StartsWith("1 file still conflicted", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    private static TestRepository TwoCommits() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"));

    private static TestRepository AheadBranch() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("two", new TestFile("b.txt", "2"))
            .Checkout("main");

    private static string FirstCommit(TestRepository repository) =>
        repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();

    private sealed class Pipeline : IDisposable
    {
        private readonly EffectRunner<TabSession> _runner;

        private Pipeline(Dispatcher host, EffectRunner<TabSession> runner, UiPump pump, StatusReporter statuses)
        {
            Host = host;
            _runner = runner;
            Pump = pump;
            Statuses = statuses;
        }

        public Dispatcher Host { get; }

        public UiPump Pump { get; }

        public StatusReporter Statuses { get; }

        public TabId TabId => Host.Workspace.ActiveTabId!.Value;

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        public bool IsRunning => Tab.Running.IsRunning(RunningOperationKind.Integration);

        public static async Task<Pipeline> OpenAsync(TestRepository repository)
        {
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var host = new Dispatcher(AppState.Empty, pump);
            var statuses = new StatusReporter(host);
            var runner = new EffectRunner<TabSession>(
                TestGit.Services,
                host,
                pump.Post,
                new TabSessionFactory(),
                statuses,
                watcherOptions: TestGit.QuietWatcherOptions,
                shell: new RecordingShell());
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            host.Dispatch(new OpenRepositoryCommand(location));
            var pipeline = new Pipeline(host, runner, pump, statuses);
            var session = runner.SessionOf(pipeline.TabId)!;
            await pump.RunUntilAsync(
                () => !session.IsLoading && pipeline.Tab.Refs.Length > 0 && pipeline.Tab.Head is not null);

            return pipeline;
        }

        public void Dispatch(AppCommand command) => Host.Dispatch(command);

        public void Dispose() => _runner.Dispose();
    }
}
