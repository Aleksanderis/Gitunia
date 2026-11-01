using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Pick;
using Gitunia.Core.Prompts;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
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
/// <param name="Prompt">The open prompt (text field or picker), or null.</param>
/// <param name="Mainline">The open mainline question of a cherry-pick or revert of a merge commit (#977), or null.</param>
/// <param name="Reflog">The reflog overlay (#984).</param>
/// <param name="Settings">The settings overlay (#984).</param>
/// <param name="Conflict">The conflict editor overlay (#982).</param>
/// <param name="Screen">The terminal size, or null until the terminal layer reported it (a dialog then counts as
/// fitting).</param>
public sealed record ShellState(
    KeyMode Mode,
    long ModeChanges,
    StatusLog Status,
    ConfirmDialog? Confirm,
    OutputOverlay? Output,
    OpenPrompt? Prompt,
    MainlineQuestion? Mainline,
    ReflogSession Reflog,
    SettingsSession Settings,
    ConflictSession Conflict,
    ScreenExtent? Screen)
{
    public static ShellState Empty { get; } = new(
        KeyMode.Normal,
        0,
        StatusLog.Empty,
        null,
        null,
        null,
        null,
        ReflogSession.Empty,
        SettingsSession.Empty,
        ConflictSession.Empty,
        null);

    /// <summary>
    /// The key mode to return to when a dialog or box opened now closes: an open dialog or box replaced by a newer one
    /// keeps its own. An open prompt is not asked: it leaves Insert mode on, and the dialog returns to it.
    /// </summary>
    public KeyMode ReturnMode
    {
        get
        {
            if (Confirm is { } confirm)
            {
                return confirm.ReturnMode;
            }

            return Output is { } output ? output.ReturnMode : Mode;
        }
    }

    /// <summary>Switches the key mode and counts the switch.</summary>
    public ShellState WithMode(KeyMode mode) => this with { Mode = mode, ModeChanges = ModeChanges + 1 };
}
