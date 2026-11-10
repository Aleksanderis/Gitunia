using Gitunia.Core.Updates;
using Gitunia.App;
using Gitunia.Tui.App.Config;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// Asks for the update hint's check (#1292): once shortly after the first frame, so startup does not wait for it, and
/// again when the settings screen changes the option. It only dispatches <see cref="UpdateCheckRequested"/>; the reducer
/// decides whether a check starts.
/// </summary>
/// <param name="host">Receives the command.</param>
/// <param name="terminal">Runs the first request after the loop has started.</param>
/// <param name="config">The option of the config file.</param>
public sealed class UpdateCheckStarter(Dispatcher host, TerminalHost terminal, LiveConfig config)
{
    /// <summary>The environment variable that turns the check off, whatever the config file says.</summary>
    public const string DisableVariable = "GITUNIA_NO_UPDATE_CHECK";

    // Long enough for the first frame to be on screen, short enough that the hint shows early.
    private static readonly TimeSpan FirstRequestDelay = TimeSpan.FromSeconds(1);

    /// <summary>The check is allowed: the option is on and the variable is not set (any non-empty value turns it off).</summary>
    /// <param name="option">The <c>updateCheck</c> option.</param>
    /// <param name="variable">The value of <see cref="DisableVariable"/>, null when unset.</param>
    public static bool IsAllowed(bool option, string? variable) => option && string.IsNullOrEmpty(variable);

    /// <summary>Requests the first check after the loop started.</summary>
    public void ScheduleFirstRequest() => terminal.RunAfter(FirstRequestDelay, Request);

    /// <summary>Requests the check (or its end) with the option as it is now.</summary>
    public void Request() =>
        host.Dispatch(new UpdateCheckRequested(
            BuildVersion.Value,
            IsAllowed(config.Options.UpdateCheck, Environment.GetEnvironmentVariable(DisableVariable))));
}
