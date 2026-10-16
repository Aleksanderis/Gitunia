using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.Input;

/// <summary>What a graph prompt needs from the shell, as delegates so the prompts run (and are tested) without Terminal.Gui.</summary>
/// <param name="ActiveSession">The active tab's session, null when there is none.</param>
/// <param name="SelectedRow">The selected commit row, -1 when nothing is selected.</param>
/// <param name="SelectRow">Selects a commit row and scrolls it into view.</param>
/// <param name="FirstVisibleRow">The first commit row in view.</param>
/// <param name="VisibleRowCount">How many rows fit in the view.</param>
/// <param name="SetMode">Switches the input mode (Insert while a prompt is open).</param>
/// <param name="Redraw">Redraws the graph and the status bar.</param>
public sealed record GraphViewHandle(
    Func<TabSession?> ActiveSession,
    Func<int> SelectedRow,
    Action<int> SelectRow,
    Func<int> FirstVisibleRow,
    Func<int> VisibleRowCount,
    Action<KeyMode> SetMode,
    Action Redraw);
