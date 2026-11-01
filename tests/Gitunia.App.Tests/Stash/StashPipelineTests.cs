using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Stash;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Stash;

// Stash actions end to end: command -> reducer -> effect -> runner -> handler -> real git -> result command.
public class StashPipelineTests
{
    [Fact]
    public async Task GivenChanges_WhenPushingThroughThePromptCommands_ThenGitHasTheStashAndTheTabIsFreeAgain()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new StashPushRequested(pipeline.TabId, false));
        Assert.Equal("Stash changes", pipeline.Host.State.Shell.Prompt!.Title);
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        foreach (var character in "hello")
        {
            pipeline.Dispatch(new PromptKeyPressedCommand(new KeyEvent(character.ToString())));
        }

        pipeline.Dispatch(new PromptAcceptedCommand());
        Assert.True(pipeline.IsRunning);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);

        Assert.Contains("hello", repository.Git("stash", "list"), StringComparison.Ordinal);
        Assert.StartsWith("Stashed \"hello\"", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenAStashRow_WhenDroppingAndAnsweringYes_ThenOnlyThatStashIsGoneAndTheListReloads()
    {
        using var repository = TestRepositories.WithStash();
        using var pipeline = await Pipeline.OpenAsync(repository);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Stashes.IsEmpty);
        var entry = pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Stashes[0];

        pipeline.Dispatch(new StashDropRequested(pipeline.TabId, entry.CommitHash));
        Assert.NotNull(pipeline.Host.State.Shell.Confirm);
        Assert.Contains("stash@{0}", repository.Git("stash", "list"), StringComparison.Ordinal);
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Stashes.IsEmpty);

        Assert.Equal(string.Empty, repository.Git("stash", "list").Trim());
        Assert.StartsWith("Dropped stash@{0}", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAStashRow_WhenTheTabIsClosedWhileTheDropDialogIsOpen_ThenYDropsNothing()
    {
        using var repository = TestRepositories.WithStash();
        using var pipeline = await Pipeline.OpenAsync(repository);
        var tabId = pipeline.TabId;
        await pipeline.Pump.RunUntilAsync(() => !pipeline.Host.Workspace.FindTab(tabId)!.Stashes.IsEmpty);
        var entry = pipeline.Host.Workspace.FindTab(tabId)!.Stashes[0];
        pipeline.Dispatch(new StashDropRequested(tabId, entry.CommitHash));
        Assert.NotNull(pipeline.Host.State.Shell.Confirm);

        pipeline.Dispatch(new CloseTabCommand(tabId));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Contains("stash@{0}", repository.Git("stash", "list"), StringComparison.Ordinal);
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

        public bool IsRunning => Host.Workspace.FindTab(TabId)!.Running.IsRunning(RunningOperationKind.Stash);

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
            await pump.RunUntilAsync(() => !session.IsLoading && host.Workspace.FindTab(pipeline.TabId)!.Refs.Length > 0);
            host.Dispatch(new TabCommand(pipeline.TabId, new FocusPaneCommand(PaneKind.Sidebar)));

            return pipeline;
        }

        public void Dispatch(AppCommand command) => Host.Dispatch(command);

        public void Dispose() => _runner.Dispose();
    }
}
