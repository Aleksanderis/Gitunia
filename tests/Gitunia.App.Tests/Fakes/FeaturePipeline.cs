using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Fakes;

/// <summary>
/// The real root dispatcher and runner over real git on a temp repository, for tests that send commands in and read
/// the state, the dialogs and the repository back.
/// </summary>
public sealed class FeaturePipeline : IDisposable
{
    private readonly EffectRunner<TabSession> _runner;

    private FeaturePipeline(Dispatcher host, EffectRunner<TabSession> runner, UiPump pump, StatusReporter statuses)
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

    public ConfirmDialog? Confirm => Host.State.Shell.Confirm;

    public KeyMode Mode => Host.State.Shell.Mode;

    public bool IsBusy => Host.Workspace.FindTab(TabId)!.Running.IsBusy;

    public static async Task<FeaturePipeline> OpenAsync(
        GitServices services,
        TestRepository repository,
        IShellEffects? shell = null)
    {
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var host = new Dispatcher(AppState.Empty, pump);
        var statuses = new StatusReporter(host);
        var runner = new EffectRunner<TabSession>(
            services,
            host,
            pump,
            new TabSessionFactory(),
            statuses,
            watcherOptions: TestGit.QuietWatcherOptions,
            shell: shell);
        var location = await services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);
        host.Dispatch(new OpenRepositoryCommand(location));
        var pipeline = new FeaturePipeline(host, runner, pump, statuses);
        var session = runner.SessionOf(pipeline.TabId)!;
        await pump.RunUntilAsync(() => !session.IsLoading && host.Workspace.FindTab(pipeline.TabId)!.Head is not null);

        return pipeline;
    }

    public void Dispatch(AppCommand command) => Host.Dispatch(command);

    public void Answer(ConfirmAnswer answer) => Host.Dispatch(new ConfirmAnsweredCommand(answer));

    public void Type(string text)
    {
        foreach (var character in text)
        {
            Host.Dispatch(new PromptKeyPressedCommand(new KeyEvent(character.ToString())));
        }
    }

    public Task UntilAsync(Func<bool> condition) => Pump.RunUntilAsync(condition);

    public void Dispose() => _runner.Dispose();
}
