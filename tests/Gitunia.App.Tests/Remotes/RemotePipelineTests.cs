using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Remotes;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Remotes;

// Remote management end to end: command -> reducer -> effect -> runner -> handler -> real git -> result command.
public class RemotePipelineTests
{
    [Fact]
    public async Task GivenNoRemote_WhenAddingThroughThePromptCommands_ThenGitHasItAndTheTabIsFreeAgain()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new RemoteActionRequested(pipeline.TabId, RemoteAction.Add, new RemoteTarget(null, null)));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Prompt is not null);
        Assert.Equal("Name of the new remote", pipeline.Prompt!.Context);
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        pipeline.Type("backup");
        pipeline.Dispatch(new PromptAcceptedCommand());
        Assert.Equal("URL of backup", pipeline.Prompt!.Context);
        pipeline.Type("https://example.com/backup.git");
        pipeline.Dispatch(new PromptAcceptedCommand());
        Assert.Null(pipeline.Prompt);
        Assert.True(pipeline.IsRunning);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);

        Assert.Equal("https://example.com/backup.git", repository.Git("config", "remote.backup.url").Trim());
        Assert.Contains("Added remote backup", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenARemote_WhenRemovingAndAnsweringYes_ThenTheRemoteAndItsTrackingBranchesAreGone()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new RemoteActionRequested(pipeline.TabId, RemoteAction.Remove, new RemoteTarget(null, null)));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Prompt is not null);
        pipeline.Dispatch(new PromptAcceptedCommand());
        var confirm = pipeline.Host.State.Shell.Confirm;
        Assert.NotNull(confirm);
        Assert.Equal("origin", repository.Git("remote").Trim());
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);

        Assert.Equal(string.Empty, repository.Git("remote").Trim());
        Assert.Equal(string.Empty, repository.Git("branch", "-r").Trim());
        Assert.Contains("Removed remote origin", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARemote_WhenSettingTheUrl_ThenThePromptStartsWithTheCurrentUrl()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new RemoteActionRequested(pipeline.TabId, RemoteAction.SetUrl, new RemoteTarget(null, null)));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Prompt is not null);
        pipeline.Dispatch(new PromptAcceptedCommand());

        Assert.Equal(repository.OriginPath, pipeline.Prompt!.Text);
        pipeline.Type("2");
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsRunning);

        Assert.Equal($"{repository.OriginPath}2", repository.Git("config", "remote.origin.url").Trim());
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

        public bool IsRunning => Host.Workspace.FindTab(TabId)!.Running.IsRunning(RunningOperationKind.Remotes);

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

            return pipeline;
        }

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
