using System.Collections.Immutable;
using Gitunia.App.Picker;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Git.Repositories;
using Xunit;

namespace Gitunia.App.Tests.Picker;

/// <summary>
/// The picker and the clone and init flow end to end: a real dispatcher, the real runner and handlers, a pump that stands
/// in for the UI thread. Tests send commands as the terminal layer does and wait on the state.
/// </summary>
public sealed class PickerPipeline : IDisposable
{
    private readonly EffectRunner<TabSession> _runner;

    private PickerPipeline(
        string home,
        ImmutableArray<string>? scanRoots,
        PickerServices picker,
        IRepositoryCreator? creator)
    {
        Home = home;
        Pump = new UiPump(TestContext.Current.CancellationToken);
        var session = PickerSession.Empty with { HomeDirectory = home, ScanRoots = scanRoots };
        Host = new Dispatcher(AppState.Empty with { Shell = ShellState.Empty with { Picker = session } }, Pump);
        Shell = new RecordingShell();
        Statuses = new StatusReporter(Host);
        var services = creator is null ? TestGit.Services : TestGit.Services with { Creator = creator };
        _runner = new EffectRunner<TabSession>(
            services,
            Host,
            Pump,
            new TabSessionFactory(),
            new EffectRunnerOptions<TabSession>
            {
                Statuses = Statuses,
                Watcher = TestGit.QuietWatcherOptions,
                Shell = Shell,
                Picker = picker,
            });
    }

    // ReSharper disable once UnusedAutoPropertyAccessor.Global : test helper, members used by some tests only
    public string Home { get; }

    public Dispatcher Host { get; }

    public UiPump Pump { get; }

    public RecordingShell Shell { get; }

    public StatusReporter Statuses { get; }

    public PickerState State => Host.State.Shell.Picker.State!;

    public bool IsOpen => Host.State.Shell.Picker.IsOpen;

    public OpenPrompt? Prompt => Host.State.Shell.Prompt;

    public bool IsCloning => Host.State.Shell.Creation.RunningClone is not null;

    public static PickerPipeline Create(
        string? home = null,
        ImmutableArray<string>? scanRoots = null,
        PickerServices? picker = null,
        IRepositoryCreator? creator = null)
    {
        // The scan finds nothing unless a test gives it folders: the home directory is the temp folder.
        return new PickerPipeline(
            home ?? Path.GetTempPath(),
            scanRoots ?? [],
            picker ?? NeverScanning(DiskDirectories.Instance),
            creator);
    }

    // Never show the scanning notice by default: only the tests about it opt in, with a zero delay.
    public static PickerServices NeverScanning(IDirectoryLister files) =>
        new(files, new PickerTimings(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan));

    public void Open(string initialQuery = "") =>
        Host.Dispatch(new PickerOpenRequested(null, initialQuery, Host.Clock.GetUtcNow()));

    public void Dispatch(AppCommand command) => Host.Dispatch(command);

    public void Type(string text) => Host.Dispatch(new TypeTextCommand(text));

    public void TypePrompt(string text)
    {
        foreach (var character in text)
        {
            Host.Dispatch(new PromptKeyPressedCommand(new KeyEvent(character.ToString())));
        }
    }

    public void ReplacePrompt(string text)
    {
        Host.Dispatch(new PromptKeyPressedCommand(new KeyEvent("u", KeyModifiers.Ctrl)));
        TypePrompt(text);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}
