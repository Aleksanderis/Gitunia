using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Worktrees;

// The worktree feature end to end: command -> reducer -> effect -> runner -> handler -> real git -> result -> refresh.
public class WorktreePipelineTests
{
    private static readonly WorktreeTarget Nothing = new(null, null, null, null, null);

    [Fact]
    public async Task GivenANewBranchName_WhenAddingThroughBothPrompts_ThenTheSiblingWorktreeExistsOnTheBranch()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 1);

        var target = new WorktreeTarget(null, null, null, null, repository.Head);

        pipeline.Dispatch(new WorktreeAddRequested(pipeline.TabId, target));
        Assert.Equal("New worktree", pipeline.Prompt!.Title);
        pipeline.Type("fresh");
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => pipeline.Prompt?.Title == "Worktree path");
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        pipeline.Dispatch(new PromptAcceptedCommand());
        Assert.True(pipeline.IsWorktreeRunning);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsWorktreeRunning && pipeline.WorktreeCount == 2);

        var path = Path.Combine(Path.GetDirectoryName(repository.WorkingTreePath)!, "fresh");
        Assert.Equal("fresh", repository.GitIn(path, ["branch", "--show-current"]).Trim());
        Assert.Equal($"Added worktree {path} on fresh", pipeline.Statuses.Log.Latest!.Text);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenAFreeBranchRow_WhenOpening_ThenOnlyThePathIsAskedAndTheNewWorktreeGetsItsOwnTab()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1")).Branch("topic");
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 1);
        var target = new WorktreeTarget(SidebarRowKind.LocalBranch, "topic", null, repository.Head, null);

        pipeline.Dispatch(new WorktreeOpenRequested(pipeline.TabId, target));
        Assert.Equal("Worktree path", pipeline.Prompt!.Title);
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.Tabs.Length == 2);

        var path = Path.Combine(Path.GetDirectoryName(repository.WorkingTreePath)!, "topic");
        var opened = pipeline.Host.Workspace.Tabs[1];
        Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(opened.Location.RootPath));
        Assert.Equal(opened.Id, pipeline.Host.Workspace.ActiveTabId);
    }

    [Fact]
    public async Task GivenACleanWorktree_WhenRemovingAndConfirming_ThenTheDirectoryIsGoneAndTheBranchKept()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 2);

        pipeline.Dispatch(new WorktreeRemoveRequested(pipeline.TabId, Pipeline.RowOf(path)));
        Assert.NotNull(pipeline.Host.State.Shell.Confirm);
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsWorktreeRunning && pipeline.WorktreeCount == 1);

        Assert.False(Directory.Exists(path));
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "wt-branch").Trim());
    }

    [Fact]
    public async Task GivenACleanWorktree_WhenTheDialogIsDeclined_ThenNothingRunsAndTheDirectoryStays()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 2);

        pipeline.Dispatch(new WorktreeRemoveRequested(pipeline.TabId, Pipeline.RowOf(path)));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.False(pipeline.IsWorktreeRunning);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task GivenADirtyWorktree_WhenTheFirstDialogIsYesAndTheSecondNo_ThenTheChangesSurvive()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        await File.WriteAllTextAsync(Path.Combine(path, "dirty.txt"), "x", TestContext.Current.CancellationToken);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 2);

        pipeline.Dispatch(new WorktreeRemoveRequested(pipeline.TabId, Pipeline.RowOf(path)));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);
        Assert.Equal("Remove worktree with changes", pipeline.Host.State.Shell.Confirm!.Prompt.Title);
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.False(pipeline.IsWorktreeRunning);
        Assert.True(File.Exists(Path.Combine(path, "dirty.txt")));
    }

    [Fact]
    public async Task GivenADirtyWorktree_WhenBothDialogsAreYes_ThenItIsForceRemoved()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        await File.WriteAllTextAsync(Path.Combine(path, "dirty.txt"), "x", TestContext.Current.CancellationToken);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 2);

        pipeline.Dispatch(new WorktreeRemoveRequested(pipeline.TabId, Pipeline.RowOf(path)));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsWorktreeRunning && pipeline.WorktreeCount == 1);

        Assert.False(Directory.Exists(path));
        Assert.Contains("and its uncommitted changes", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenATooSmallScreen_WhenRemoving_ThenNoDialogOpensAndTheWorktreeStays()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 2);
        pipeline.Dispatch(new ScreenResizedCommand(new ScreenExtent(1, 1), pipeline.Host.Clock.GetUtcNow()));

        pipeline.Dispatch(new WorktreeRemoveRequested(pipeline.TabId, Pipeline.RowOf(path)));

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Contains("nothing was changed", pipeline.Statuses.Current!.Text, StringComparison.Ordinal);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task GivenAPrunableEntry_WhenPruningAndConfirming_ThenOnlyItsEntryGoes()
    {
        using var repository = TestRepositories.WithManyWorktrees(out var lockedPath, out var prunablePath, out _);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 4);

        pipeline.Dispatch(new WorktreePruneRequested(pipeline.TabId));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsWorktreeRunning && pipeline.WorktreeCount == 3);

        var listed = repository.Git("worktree", "list", "--porcelain");
        Assert.DoesNotContain(prunablePath, listed, StringComparison.Ordinal);
        Assert.Contains(lockedPath, listed, StringComparison.Ordinal);
        Assert.Equal("Pruned 1 worktree entry", pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAWorktreeRow_WhenSwitching_ThenTheTabMovesThereAndKeepsItsId()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 2);
        var tabId = pipeline.TabId;

        pipeline.Dispatch(new WorktreeSwitchRequested(tabId, Pipeline.RowOf(path)));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.FindTab(tabId)!.CommandPath == path);

        Assert.Single(pipeline.Host.Workspace.Tabs);
        Assert.Equal($"Switched to worktree {path}", pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAWorktreeRow_WhenOpeningTwice_ThenItGetsOneTabAndTheSecondOpenFocusesIt()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 2);
        var first = pipeline.TabId;

        pipeline.Dispatch(new WorktreeOpenRequested(first, Pipeline.RowOf(path)));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.Tabs.Length == 2);
        pipeline.Dispatch(new ActivateTabCommand(first));
        pipeline.Dispatch(new WorktreeOpenRequested(first, Pipeline.RowOf(path)));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.ActiveTabId != first);

        Assert.Equal(2, pipeline.Host.Workspace.Tabs.Length);
    }

    [Fact]
    public async Task GivenTheRemoveDialogOpen_WhenTheTabIsClosed_ThenYRemovesNothing()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 2);
        var tabId = pipeline.TabId;
        pipeline.Dispatch(new WorktreeRemoveRequested(tabId, Pipeline.RowOf(path)));
        pipeline.Dispatch(new CloseTabCommand(tabId));

        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task GivenAGitFailure_WhenAdding_ThenTheErrorIsShownAndTheTabIsFreeAgain()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 1);
        var occupied = Path.Combine(Path.GetDirectoryName(repository.WorkingTreePath)!, "occupied");
        Directory.CreateDirectory(occupied);
        await File.WriteAllTextAsync(Path.Combine(occupied, "x.txt"), "x", TestContext.Current.CancellationToken);

        pipeline.Dispatch(new WorktreePathAccepted(pipeline.TabId, "fresh", repository.Head, false, occupied));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsWorktreeRunning);

        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
        Assert.StartsWith("Could not add the worktree", pipeline.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(occupied, ".git")));
    }

    [Fact]
    public async Task GivenNoSelection_WhenAdding_ThenThePromptOpensAndNothingElseHappens()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        using var pipeline = await Pipeline.OpenAsync(repository, expectedWorktrees: 1);

        pipeline.Dispatch(new WorktreeAddRequested(pipeline.TabId, Nothing));

        Assert.NotNull(pipeline.Prompt);
        Assert.False(pipeline.IsWorktreeRunning);
    }

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

        public OpenPrompt? Prompt => Host.State.Shell.Prompt;

        public bool IsWorktreeRunning =>
            Host.Workspace.FindTab(TabId)!.Running.IsRunning(RunningOperationKind.Worktrees);

        public int WorktreeCount => Host.Workspace.FindTab(TabId)!.Worktrees.Length;

        public static async Task<Pipeline> OpenAsync(TestRepository repository, int expectedWorktrees)
        {
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var host = new Dispatcher(AppState.Empty, pump);
            var statuses = new StatusReporter(host);
            var runner = new EffectRunner<TabSession>(
                TestGit.Services,
                host,
                pump,
                new TabSessionFactory(),
                statuses,
                watcherOptions: TestGit.QuietWatcherOptions);
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            host.Dispatch(new OpenRepositoryCommand(location));
            var pipeline = new Pipeline(host, runner, pump, statuses);
            var session = runner.SessionOf(pipeline.TabId)!;
            await pump.RunUntilAsync(() => !session.IsLoading && pipeline.WorktreeCount == expectedWorktrees);

            return pipeline;
        }

        public static WorktreeTarget RowOf(string path) =>
            new(SidebarRowKind.Worktree, Path.GetFileName(path), path, null, null);

        public void Dispatch(AppCommand command) => Host.Dispatch(command);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Host.Dispatch(new PromptKeyPressedCommand(new KeyEvent(character.ToString())));
            }
        }

        public void Dispose() => _runner.Dispose();
    }
}
