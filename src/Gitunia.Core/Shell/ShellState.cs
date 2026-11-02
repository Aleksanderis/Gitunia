using Gitunia.Core.Compare;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Creation;
using Gitunia.Core.Jump;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Picker;
using Gitunia.Core.Pick;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;

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
/// <param name="RebaseEditor">The interactive rebase editor (#983).</param>
/// <param name="Reflog">The reflog overlay (#984).</param>
/// <param name="Settings">The settings overlay (#984).</param>
/// <param name="Conflict">The conflict editor overlay (#982).</param>
/// <param name="Creation">Clone and init from the repository picker (#985).</param>
/// <param name="Picker">The repository picker (#985).</param>
/// <param name="Compare">What the comparison of commits remembers per tab (#996).</param>
/// <param name="QuickJump">The open quick jump prompt, or null (#996).</param>
/// <param name="Sidebar">What the sidebar keys keep between presses: the filter being typed (#996).</param>
/// <param name="Overlay">Which of the help, palette, actions menu or history the overlay layer shows, or none (#1020);
/// the confirm dialog and the output box are the fields above.</param>
/// <param name="OverlayReturnMode">The key mode <paramref name="Overlay"/> was opened from; read only while one is
/// open.</param>
/// <param name="QuitWarned">The first quit after a failed save warned; the next one leaves.</param>
/// <param name="TabActions">What the tab and pin actions keep between presses (#985).</param>
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
    RebaseEditorSession RebaseEditor,
    ReflogSession Reflog,
    SettingsSession Settings,
    ConflictSession Conflict,
    CreationSession Creation,
    PickerSession Picker,
    CompareSession Compare,
    QuickJumpSession? QuickJump,
    SidebarSession Sidebar,
    bool QuitWarned,
    WorkspaceActionSession TabActions,
    ScreenExtent? Screen,
    VisualSelection? Visual = null,
    OverlayKind Overlay = OverlayKind.None,
    KeyMode OverlayReturnMode = KeyMode.Normal)
{
    public static ShellState Empty { get; } = new(
        KeyMode.Normal,
        0,
        StatusLog.Empty,
        null,
        null,
        null,
        null,
        RebaseEditorSession.Empty,
        ReflogSession.Empty,
        SettingsSession.Empty,
        ConflictSession.Empty,
        CreationSession.Empty,
        PickerSession.Empty,
        CompareSession.Empty,
        null,
        SidebarSession.Empty,
        false,
        WorkspaceActionSession.Empty,
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

            if (Output is { } output)
            {
                return output.ReturnMode;
            }

            return Overlay == OverlayKind.None ? Mode : OverlayReturnMode;
        }
    }

    /// <summary>Switches the key mode and counts the switch.</summary>
    public ShellState WithMode(KeyMode mode) => this with { Mode = mode, ModeChanges = ModeChanges + 1 };
}
