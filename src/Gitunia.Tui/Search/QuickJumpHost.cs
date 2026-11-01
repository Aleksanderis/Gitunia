using Gitunia.App;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.Search;

/// <summary>What the quick jump prompt needs from the shell, as delegates so it runs (and is tested) without Terminal.Gui.</summary>
/// <param name="ActiveSession">The active tab's session, null when there is none.</param>
/// <param name="FocusedPane">The focused pane of the active tab.</param>
/// <param name="OpenTarget">Creates the target for the focused pane, null when that pane has no quick jump.</param>
/// <param name="Dispatcher">Where the prompt switches the input mode (Insert while it is open).</param>
/// <param name="Redraw">Redraws the status bar (the lists redraw themselves through their target).</param>
public sealed record QuickJumpHost(
    Func<TerminalTabSession?> ActiveSession,
    Func<PaneKind> FocusedPane,
    Func<IJumpTarget?> OpenTarget,
    Dispatcher Dispatcher,
    Action Redraw);
