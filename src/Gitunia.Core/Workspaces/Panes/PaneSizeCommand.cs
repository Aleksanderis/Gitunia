using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Workspaces.Panes;

/// <summary>
/// A pane resize key (<c>Ctrl-w &gt;</c>, <c>&lt;</c>, <c>+</c>, <c>-</c>, <c>=</c>) for one tab. The sizes, the focused
/// pane and the screen come from the state; what only the terminal layer knows travels with the command.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="Action">One of <c>pane.wider</c>, <c>pane.narrower</c>, <c>pane.taller</c>, <c>pane.shorter</c>,
/// <c>pane.reset-sizes</c>.</param>
/// <param name="Count">The typed count, null for one step.</param>
/// <param name="Layout">The arrangement the panes have now.</param>
public sealed record PaneSizeCommand(TabId TabId, ActionId Action, int? Count, PaneLayoutFacts Layout) : AppCommand;
