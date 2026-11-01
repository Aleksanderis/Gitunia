using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Tags;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests;

// The pilot feature (tags) end to end: command -> reducer -> effect -> runner -> handler -> real git -> result command.
public class EffectPipelineTests
{
    [Fact]
    public async Task GivenACommit_WhenCreatingATagThroughCommands_ThenGitHasItAndTheStateIsFreeAgain()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new TagCreateRequested(pipeline.TabId, new TagTarget(null, null, repository.Head)));
        pipeline.Dispatch(new TagPromptTextChanged(pipeline.TabId, "v1"));
        pipeline.Dispatch(new TagPromptAccepted(pipeline.TabId));
        Assert.Equal(TagPromptKind.CreateMessage, pipeline.Tags.Prompt!.Kind);
        pipeline.Dispatch(new TagPromptAccepted(pipeline.TabId));
        Assert.True(pipeline.Tags.Running);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.Tags.Running);

        Assert.Equal(repository.Head, repository.Git("rev-parse", "v1").Trim());
        Assert.Null(pipeline.Tags.Prompt);
        Assert.Contains("lightweight tag v1", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal([KeyMode.Insert, KeyMode.Insert, KeyMode.Normal], pipeline.Shell.Modes);
    }

    [Fact]
    public async Task GivenATag_WhenDeletingItThroughCommands_ThenTheConfirmCarriesTheCommandThatDeletesIt()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        var target = new TagTarget(SidebarRowKind.Tag, "v1", repository.Head);

        pipeline.Dispatch(new TagDeleteRequested(pipeline.TabId, target));
        var confirm = Assert.Single(pipeline.Shell.Confirms);
        Assert.Equal("Delete tag", confirm.Prompt.Title);
        Assert.Equal("v1", repository.Git("tag", "--list").Trim());
        pipeline.Dispatch(confirm.OnAccepted);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.Tags.Running);

        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
        Assert.Contains("Deleted tag v1", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARunningTagOperation_WhenAnotherIsRequested_ThenItIsRefusedAsBusy()
    {
        using var repository = TestRepositories.Linear();
        var writer = new BlockingTagWriter();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services with { Tags = writer }, repository);
        pipeline.Dispatch(new TagDeleteConfirmed(pipeline.TabId, "v1", repository.Head));
        Assert.True(pipeline.Tags.Running);

        pipeline.Dispatch(new TagCreateRequested(pipeline.TabId, new TagTarget(null, null, repository.Head)));

        Assert.Null(pipeline.Tags.Prompt);
        Assert.Equal(TagReducer.BusyNotice, pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenARunningTagOperation_WhenItsTabIsClosed_ThenItsWorkIsCancelled()
    {
        using var repository = TestRepositories.Linear();
        var writer = new BlockingTagWriter();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services with { Tags = writer }, repository);
        pipeline.Dispatch(new TagDeleteConfirmed(pipeline.TabId, "v1", repository.Head));
        await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        pipeline.Dispatch(new CloseTabCommand(pipeline.TabId));

        Assert.True(writer.Token.IsCancellationRequested);
        await pipeline.Pump.RunUntilAsync(() => writer.Finished);
    }

    [Fact]
    public async Task GivenAHandlerThatThrows_WhenRunning_ThenAnErrorIsPostedInsteadOfTheExceptionEscaping()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Shell.ThrowOnKeyMode = true;

        pipeline.Dispatch(new TagCreateRequested(pipeline.TabId, new TagTarget(null, null, repository.Head)));

        Assert.StartsWith("Internal error (InvalidOperationException):", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest.Severity);
    }

    [Fact]
    public void GivenEveryConcreteEffect_WhenRunning_ThenTheRunnerHasACaseForIt()
    {
        var effects = ConcreteSubtypes.InstancesOf<AppEffect>();
        Assert.Contains(effects, effect => effect is TagEffect);
        Assert.Contains(effects, effect => effect is PostStatusEffect);
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var host = new Dispatcher(AppState.Empty, pump);
        using var runner = new EffectRunner<TabSession>(
            new FakeGitServices().Services,
            host,
            pump.Post,
            new TabSessionFactory(),
            new StatusChannel(),
            watcherOptions: TestGit.QuietWatcherOptions);

        foreach (var effect in effects)
        {
            // An empty effect may fail inside its handler (that becomes a failure command); only a missing case is a bug.
            Assert.False(MissesItsCase(runner, effect), $"{effect.GetType().Name} has no case in EffectRunner");
        }
    }

    [Fact]
    public async Task GivenAPushThatNeedsCredentials_WhenItFailsThroughTheRunner_ThenTheShellOffersTheTerminalRetry()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        var writer = new AuthFailingTagWriter(TestGit.Services.Tags, "git@host: Permission denied (publickey).");
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services with { Tags = writer }, repository);
        pipeline.Dispatch(new TagPushRequested(pipeline.TabId, new TagTarget(SidebarRowKind.Tag, "v1", repository.Head)));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Tags.Prompt is not null);

        pipeline.Dispatch(new TagPromptAccepted(pipeline.TabId));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.Tags.Running);

        Assert.Equal(["Tag push|push -- origin refs/tags/v1"], pipeline.Shell.Retries);
        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
    }

    private static bool MissesItsCase(EffectRunner<TabSession> runner, AppEffect effect)
    {
        try
        {
            runner.Run(effect);
        }
        catch (UnhandledEffectException)
        {
            return true;
        }

        return false;
    }

    private sealed class Pipeline : IDisposable
    {
        private readonly EffectRunner<TabSession> _runner;

        private Pipeline(
            Dispatcher host,
            EffectRunner<TabSession> runner,
            UiPump pump,
            RecordingShell shell,
            StatusChannel statuses)
        {
            Host = host;
            _runner = runner;
            Pump = pump;
            Shell = shell;
            Statuses = statuses;
        }

        public Dispatcher Host { get; }

        public UiPump Pump { get; }

        public RecordingShell Shell { get; }

        public StatusChannel Statuses { get; }

        public TabId TabId => Host.Workspace.ActiveTabId!.Value;

        public bool HasRefs => Host.Workspace.FindTab(TabId)!.Refs.Length > 0;

        public TagState Tags => Host.Workspace.FindTab(TabId)!.Tags;

        public static async Task<Pipeline> OpenAsync(GitServices services, TestRepository repository)
        {
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var host = new Dispatcher(AppState.Empty, pump);
            var shell = new RecordingShell();
            var statuses = new StatusChannel();
            var runner = new EffectRunner<TabSession>(
                services,
                host,
                pump.Post,
                new TabSessionFactory(),
                statuses,
                watcherOptions: TestGit.QuietWatcherOptions,
                shell: shell);
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            host.Dispatch(new OpenRepositoryCommand(location));
            var pipeline = new Pipeline(host, runner, pump, shell, statuses);
            var session = runner.SessionOf(pipeline.TabId)!;
            await pump.RunUntilAsync(() => !session.IsLoading && pipeline.HasRefs);

            return pipeline;
        }

        public void Dispatch(AppCommand command) => Host.Dispatch(command);

        public void Dispose() => _runner.Dispose();
    }
}
