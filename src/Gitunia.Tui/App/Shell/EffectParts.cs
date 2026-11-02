using Gitunia.Git.Watching;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Config;
using Gitunia.App.Worktrees;
using Gitunia.App;
using Gitunia.App.Settings;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings;
using Gitunia.Git.Watching.Models;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The last stage: the effect runner with the effects only the terminal can perform, the session saving and the pollers
/// of the other tabs. The runner needs the shell effects at construction, and they need everything built before.
/// </summary>
public sealed class EffectParts : IDisposable
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);

    /// <param name="foundation">The configuration, the state and the widgets.</param>
    /// <param name="panes">The controllers of the panes.</param>
    /// <param name="keys">The key controller and the overlay controller.</param>
    /// <param name="handlers">The action handlers.</param>
    /// <param name="screen">The main view.</param>
    public EffectParts(
        ShellFoundation foundation,
        PaneParts panes,
        KeyParts keys,
        HandlerParts handlers,
        ScreenParts screen)
    {
        var host = foundation.Host;
        var statuses = foundation.Statuses;
        var terminal = foundation.Terminal;
        var configWriter = new ConfigWriter(foundation.Platform.ConfigDirectory);
        Persistence = new SessionPersistence(foundation.Store, host, SaveDebounce, statuses, terminal.Invoker);
        var options = new OptionsApplier(
            foundation.Config,
            new OptionTargets(keys.Overlays, panes.Picker, terminal, screen.View),
            foundation.SessionFactory,
            foundation.Registry,
            foundation.Panes);
        var parts = new ShellEffectParts(
            new CursorEffects(host, panes.WorkingDiff, panes.QuickJump, panes.Graph, panes.Detail),
            new StashEffects(panes.StashView, panes.Rows),
            new ToolEffects(handlers.Tools, handlers.ConfigEditor, configWriter, statuses, host),
            new CopyEffects(foundation.Clipboard, handlers.RangeCopier, handlers.Retry, host),
            new SessionEffects(Persistence, terminal, options),
            new FlowEffects(handlers.ReflogEntries, handlers.Staging, host, statuses, panes.DiffPane));
        Runner = new EffectRunner<TerminalTabSession>(
            foundation.Services,
            host,
            terminal.Invoker,
            foundation.SessionFactory,
            new EffectRunnerOptions<TerminalTabSession>
            {
                Statuses = statuses,
                Watcher = WatcherOptionsFor(foundation.Config.Options),
                Shell = new ShellEffects(parts, handlers.Actions),
                Settings = CreateSettingsServices(foundation, configWriter),
                Sessions = foundation.Registry,
            });
        StatusPoller = new BackgroundStatusPoller(
            foundation.Services.Status,
            host,
            terminal.Invoker,
            background: Runner.Background);
        WorktreePoller = new WorktreeOverviewPoller(foundation.Services.Status, host, background: Runner.Background);
    }

    /// <summary>Saves the session with a debounce and at the end.</summary>
    public SessionPersistence Persistence { get; }

    /// <summary>Runs the effects the reducers return.</summary>
    public EffectRunner<TerminalTabSession> Runner { get; }

    /// <summary>Reads the status of the tabs that are not shown.</summary>
    public BackgroundStatusPoller StatusPoller { get; }

    /// <summary>Reads the overview of the worktrees.</summary>
    public WorktreeOverviewPoller WorktreePoller { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        StatusPoller.Dispose();
        WorktreePoller.Dispose();
        Runner.Dispose();
        Persistence.Dispose();
    }

    private static SettingsServices CreateSettingsServices(ShellFoundation foundation, ConfigWriter configWriter)
    {
        var startup = foundation.Config.Startup;

        return new SettingsServices(
            configWriter,
            new SettingsEnvironment(
                foundation.Config.HomeDirectory,
                Directory.Exists,
                startup.Themes.Names,
                ConfigStartup.DefaultLabelCaps(foundation.Config.Capabilities)),
            SettingsRegistry.WithThemes(startup.Themes.Names),
            startup.Keymap.Table);
    }

    // Polling is the default; watching is opt-in and capped by the inotify instance limit (read at startup only).
    private static RepositoryWatcherOptions WatcherOptionsFor(ConfigOptions options)
    {
        var defaults = RepositoryWatcherOptions.Default;
        if (!options.WatchFiles)
        {
            return defaults;
        }

        var instances = InotifyLimit.ReadMaximumInstances();
        var cap = InotifyLimit.DirectoryCap(instances, defaults.MaximumWorkingTreeDirectories);

        return defaults with { UseFileWatchers = true, MaximumWorkingTreeDirectories = cap };
    }
}
