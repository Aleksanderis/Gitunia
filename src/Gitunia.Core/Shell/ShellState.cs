using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;

namespace Gitunia.Core.Shell;

/// <summary>
/// The app-wide interaction state that is not tied to one tab: the key mode, the status messages, the confirm dialog
/// and the output overlay. Changed only by <see cref="ShellReducer"/>.
/// </summary>
/// <param name="Mode">The input mode of the keymap.</param>
/// <param name="ModeChanges">Counts mode switches, so the key controller drops a pending chord on every one, also a
/// switch to the same mode.</param>
/// <param name="Status">The status messages and which one still shows.</param>
/// <param name="Confirm">The open confirm dialog, or null.</param>
/// <param name="Output">The open output overlay, or null.</param>
/// <param name="Screen">The terminal size, or null until the terminal layer reported it (a dialog then counts as
/// fitting).</param>
public sealed record ShellState(
    KeyMode Mode,
    long ModeChanges,
    StatusLog Status,
    ConfirmDialog? Confirm,
    OutputOverlay? Output,
    ScreenExtent? Screen)
{
    public static ShellState Empty { get; } = new(KeyMode.Normal, 0, StatusLog.Empty, null, null, null);
}
