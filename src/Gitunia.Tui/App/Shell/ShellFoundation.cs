using Gitunia.Core.Settings;
using Gitunia.Core.Picker;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Persistence;
using Gitunia.App;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.Shell;
using Gitunia.Platform;
using Gitunia.Platform.Terminal;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;
using Gitunia.Core.Keymap;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The first stage of the composition root: the configuration, the terminal, the state, the sessions of the tabs and the
/// widgets of the panes. Everything later is built from these and never creates them again.
/// </summary>
public sealed class ShellFoundation
{
    /// <param name="services">The git readers and writers.</param>
    /// <param name="platform">The platform's directories and terminal queries.</param>
    /// <param name="detected">What the terminal can show, before the theme query.</param>
    /// <param name="launch">How the shell was asked to start.</param>
    public ShellFoundation(GitServices services, IPlatform platform, ConsoleCapabilities detected, TuiLaunch launch)
    {
        Services = services;
        Platform = platform;
        GitExecutable = launch.GitExecutable;

        // First of all: the background query below makes .NET switch the terminal to no-echo, line-less input.
        var handOff = new TerminalHandOff(CaptureTerminalModes());
        var home = launch.HomeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var startup = new ConfigStartup(platform.ConfigDirectory, home, ConfigStartup.DefaultLabelCaps(detected)).Load();
        Config = new LiveConfig(startup, WithBackground(detected, startup, platform), home);
        Keys = new ChipKeys(startup.Keymap.Table);
        Engine = new KeymapEngine(startup.Keymap.Table);
        Store = new SessionStore(platform.StateDirectory);
        // Startup, before the UI runs: a one-off blocking read of the small session file.
        Restored = Store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Terminal = new TerminalHost(launch.DriverName, Config, handOff);
        Host = new Dispatcher(InitialState(), Terminal.Invoker);
        Statuses = new StatusReporter(Host);
        Registry = new TabSessionRegistry<TerminalTabSession>();
        Sessions = new ActiveSessions(Host, Registry);
        SessionFactory = new TerminalTabSessionFactory(Config.Glyphs, new StagingButtonSource(Host));
        Clipboard = CreateClipboard();
        Chrome = new PaneChrome();
        Gate = new MouseGate();
        Arrangements = new PaneArrangements(Host, Sessions);
        WorkingFocus = new WorkingTreeFocus(Host, Sessions);
        Geometry = new PaneGeometry(Chrome, Arrangements, WorkingFocus);
        Panes = new PaneViews(
            Chrome,
            Geometry,
            Gate,
            new PaneViewContent(Config.Glyphs, Keys, new SidebarListSource(Config.Glyphs), new StateMessageBoxPort(Host)));
    }

    /// <summary>The git readers and writers.</summary>
    public GitServices Services { get; }

    /// <summary>The git executable located at startup, for hand-offs to the terminal.</summary>
    public string GitExecutable { get; }

    /// <summary>The platform's directories and terminal queries.</summary>
    public IPlatform Platform { get; }

    /// <summary>The configuration the running app follows.</summary>
    public LiveConfig Config { get; }

    /// <summary>The keys the chips and hints show.</summary>
    public ChipKeys Keys { get; }

    /// <summary>The keymap engine.</summary>
    public KeymapEngine Engine { get; }

    /// <summary>Where the session is saved.</summary>
    public SessionStore Store { get; }

    /// <summary>The session of the last run.</summary>
    public SessionLoadResult Restored { get; }

    /// <summary>The terminal application.</summary>
    public TerminalHost Terminal { get; }

    /// <summary>The state.</summary>
    public Dispatcher Host { get; }

    /// <summary>Posts status messages.</summary>
    public StatusReporter Statuses { get; }

    /// <summary>The runtime sessions of the open tabs.</summary>
    public TabSessionRegistry<TerminalTabSession> Registry { get; }

    /// <summary>The runtime session of the shown tab.</summary>
    public ActiveSessions Sessions { get; }

    /// <summary>Builds the sessions of new tabs.</summary>
    public TerminalTabSessionFactory SessionFactory { get; }

    /// <summary>Copies text to the clipboard.</summary>
    public ClipboardCopier Clipboard { get; }

    /// <summary>The window and the two requests the pane controllers make of the screen.</summary>
    public PaneChrome Chrome { get; }

    /// <summary>Tells the widgets whether an overlay owns the mouse.</summary>
    public MouseGate Gate { get; }

    /// <summary>Which panes are shown and how big.</summary>
    public PaneArrangements Arrangements { get; }

    /// <summary>What the Changes view layout reads from the tab.</summary>
    public WorkingTreeFocus WorkingFocus { get; }

    /// <summary>Where the panes are.</summary>
    public PaneGeometry Geometry { get; }

    /// <summary>The list widgets.</summary>
    public PaneViews Panes { get; }

    // Taken before the driver starts, while the terminal is as the shell left it.
    private static TerminalModes CaptureTerminalModes()
    {
        var modes = new TerminalModes();
        _ = modes.Capture();

        return modes;
    }

    private static ConsoleCapabilities WithBackground(
        ConsoleCapabilities detected,
        StartupConfig startup,
        IPlatform platform)
    {
        if (startup.Theme == ThemeChoice.Auto && platform.QueryLightBackground(detected) is { } isLight)
        {
            return detected with { IsLightBackground = isLight };
        }

        return detected;
    }

    // The initial state: nothing open, and the home folder the settings screen expands `~` with.
    private AppState InitialState()
    {
        var settings = SettingsSession.Empty with { HomeDirectory = Config.HomeDirectory };
        var picker = PickerSession.Empty with
        {
            HomeDirectory = Config.HomeDirectory,
            ScanRoots = Config.Options.ScanRoots,
        };

        return AppState.Empty with { Shell = ShellState.Empty with { Settings = settings, Picker = picker } };
    }

    private ClipboardCopier CreateClipboard()
    {
        var clipboardWriter = default(ClipboardWriter);
        if (Config.Capabilities.IsInteractive)
        {
            var environment = Osc52Environment.Detect(Environment.GetEnvironmentVariable);
            clipboardWriter = new ClipboardWriter(Console.Out, environment);
        }

        return new ClipboardCopier(clipboardWriter, Statuses);
    }
}
