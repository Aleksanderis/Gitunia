using Gitunia.Core.Workspaces;
using Gitunia.Core.Commits;
using Gitunia.App;
using Gitunia.App.Picker;
using Gitunia.App.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Shell;

namespace Gitunia.Tui.App;

/// <summary>
/// The entry of the terminal shell: composes the objects (<see cref="ShellComposition"/>), connects them
/// (<see cref="ShellWiring"/>), restores the session and runs the main loop. Keys go through the keymap engine, actions
/// become workspace commands, git results come back on the UI thread.
/// </summary>
public sealed class TuiApplication(GitServices services, ConsoleCapabilities detectedCapabilities, IPlatform platform)
{
    private static readonly TimeSpan ExistenceTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Terminal.Gui driver to force (smoke tests); null picks the platform default.</summary>
    public string? DriverName { get; init; }

    /// <summary>Home directory for config paths and picker scans (smoke tests); null uses the user profile.</summary>
    public string? HomeDirectory { get; init; }

    /// <summary>The located git executable for terminal hand-offs (difftool, mergetool, credential retry).</summary>
    public string GitExecutable { get; init; } = GitExecutableLocator.BareName;

    /// <summary>Stop the main loop right after it starts, so a smoke test builds the shell and returns.</summary>
    public bool ExitAfterFirstIteration { get; init; }

    /// <summary>
    /// Smoke tests: receives the screen text after one forced draw in the first iteration (the loop stops right
    /// after). Only used with <see cref="ExitAfterFirstIteration"/>.
    /// </summary>
    public Action<string>? FrameCaptured { get; init; }

    /// <summary>Runs the UI until the user quits.</summary>
    /// <param name="location">A repository to open on top of the restored session, null for none.</param>
    public int Run(RepositoryLocation? location)
    {
        using var shell = new ShellComposition(
            services,
            platform,
            detectedCapabilities,
            new TuiLaunch(DriverName, HomeDirectory, GitExecutable));
        new ShellWiring(shell).Connect();
        Start(shell, location);
        try
        {
            shell.Foundation.Terminal.Run(shell.Foundation.Chrome.Window);
        }
        finally
        {
            shell.Effects.StatusPoller.Dispose();
            shell.Effects.WorktreePoller.Dispose();
            shell.Effects.Persistence.Flush();
        }

        return 0;
    }

    private void Start(ShellComposition shell, RepositoryLocation? location)
    {
        var foundation = shell.Foundation;
        var host = foundation.Host;
        foundation.Terminal.StartFocusReporting();
        if (foundation.Restored.Detail is { Length: > 0 } restoreNotice)
        {
            foundation.Statuses.Warning(restoreNotice);
        }

        ConfigStartup.Report(foundation.Config.Keymap.Problems, foundation.Statuses);
        host.Restore(foundation.Restored.Update);
        foreach (var (tabId, saved) in foundation.Restored.Drafts)
        {
            host.Dispatch(new CommitDraftRestored(tabId, saved));
        }

        // The restored tabs are not probed on the disk before the first frame; a dead path is marked missing a moment later.
        _ = new TabExistenceChecker(host, DiskDirectories.Instance, ExistenceTimeout)
            .Start(host.Workspace.Tabs, CancellationToken.None);
        shell.Effects.StatusPoller.Start();
        shell.Effects.WorktreePoller.Start();
        if (location is not null)
        {
            host.Dispatch(new OpenRepositoryCommand(location));
        }

        shell.Screen.Binder.Sync();
        if (host.Workspace.Tabs.IsEmpty)
        {
            shell.Panes.Picker.Open();
        }

        shell.Effects.Persistence.Start();
        shell.Effects.UpdateChecks.ScheduleFirstRequest();
        foundation.Panes.Graph.SetFocus();
        if (ExitAfterFirstIteration)
        {
            foundation.Terminal.StopAfterFirstFrame(FrameCaptured);
        }
    }
}
