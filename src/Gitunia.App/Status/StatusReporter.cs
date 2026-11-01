using Gitunia.Core.Shell;
using Gitunia.Core.Status;

namespace Gitunia.App.Status;

/// <summary>
/// Where anything that fails or finishes reports to the status bar. The messages themselves are state
/// (<see cref="ShellState.Status"/>, changed by the shell reducer); this class stamps the time, dispatches the command
/// and reads the current message back. UI thread only, like <see cref="Dispatcher.Dispatch"/>.
/// </summary>
/// <param name="host">The dispatcher that holds the state.</param>
public sealed class StatusReporter(Dispatcher host)
{
    /// <summary>The status messages as of now (the history and which one is showing).</summary>
    public StatusLog Log => host.State.Shell.Status;

    /// <summary>The message the status bar shows now: the newest one unless it was dismissed or has expired.</summary>
    public StatusMessage? Current => Log.Current(host.Clock.GetUtcNow());

    /// <summary>Whether <see cref="Current"/> was posted so recently that it is drawn to catch the eye.</summary>
    public bool IsFresh => Current is { } message && StatusLifetime.IsFresh(message, host.Clock.GetUtcNow());

    public void Post(string text, StatusSeverity severity = StatusSeverity.Info) =>
        host.Dispatch(new StatusPostedCommand(text, severity, host.Clock.GetUtcNow()));

    public void Error(string text) => Post(text, StatusSeverity.Error);

    public void Warning(string text) => Post(text, StatusSeverity.Warning);

    /// <summary>Hides the current message. Returns false when none was showing (expired counts as gone).</summary>
    public bool Dismiss()
    {
        if (Current is null)
        {
            return false;
        }

        host.Dispatch(new StatusDismissedCommand(host.Clock.GetUtcNow()));

        return true;
    }
}
