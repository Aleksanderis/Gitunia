using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;

namespace Gitunia.App.Shell;

/// <summary>
/// What non-reducer code uses to reach the shell state: each call dispatches the matching shell command.
/// </summary>
public static class ShellDispatch
{
    /// <summary>Switches the keymap mode (Insert while a prompt is open); a pending chord is dropped.</summary>
    public static void SetKeyMode(this Dispatcher host, KeyMode mode) => host.Dispatch(new SetKeyModeCommand(mode));

    /// <summary>Shows the output of a failed command (a failing commit hook) in a scrollable box.</summary>
    public static void ShowOutput(this Dispatcher host, string title, string heading, IReadOnlyList<string> lines) =>
        host.Dispatch(new OutputShownCommand(title, heading, lines));
}
