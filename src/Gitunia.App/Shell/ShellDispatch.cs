using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;

namespace Gitunia.App.Shell;

/// <summary>
/// What controllers that have not moved to commands yet use to reach the shell state: each call dispatches the
/// matching shell command. A controller that has moved dispatches the command itself.
/// </summary>
public static class ShellDispatch
{
    /// <summary>Switches the keymap mode (Insert while a prompt is open); a pending chord is dropped.</summary>
    public static void SetKeyMode(this Dispatcher host, KeyMode mode) => host.Dispatch(new SetKeyModeCommand(mode));

    /// <summary>
    /// Asks the user before a destructive action: opens the dialog and switches to Confirm mode, where only <c>y</c>
    /// runs <paramref name="onConfirmed"/>; <c>n</c> and <c>Esc</c> drop it. Returns false (and shows nothing) when the
    /// dialog does not fit the screen: the caller must then not start the action.
    /// </summary>
    public static bool Confirm(this Dispatcher host, ConfirmPrompt prompt, Action onConfirmed)
    {
        host.Dispatch(new ConfirmRequestedCommand(prompt, new RunActionCommand(onConfirmed), null));

        return ReferenceEquals(host.State.Shell.Confirm?.Prompt, prompt);
    }

    /// <summary>Shows the output of a failed command (a failing commit hook) in a scrollable box.</summary>
    public static void ShowOutput(this Dispatcher host, string title, string heading, IReadOnlyList<string> lines) =>
        host.Dispatch(new OutputShownCommand(title, heading, lines));
}
