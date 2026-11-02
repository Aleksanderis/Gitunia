using Gitunia.Tui.Rendering.Diff;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Views;

/// <summary>What the layout of the Changes view reads from the active tab: the focused pane, the diff's side and zoom.</summary>
public interface IWorkingTreeFocus
{
    /// <summary>
    /// The pane of the active tab that has focus in the Changes view: a list, a diff or the message box, the Unstaged
    /// list when the focus is elsewhere.
    /// </summary>
    PaneKind FocusedPane();

    /// <summary>The keymap view of the diff box (the side of the selected file), for the mouse map.</summary>
    string DiffView();

    /// <summary>True while the diff box fills the whole Changes view (<c>&lt;space&gt;z</c>).</summary>
    bool IsDiffZoomed();

    /// <summary>The staging buttons of the diff box's title row: those of the selected file's side.</summary>
    WorkingTreeButtons DiffButtons();
}
