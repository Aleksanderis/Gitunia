using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Visual;

/// <summary>The Visual-mode range that is active: a range of rows anchored at the cursor of one pane of one tab.</summary>
/// <param name="TabId">The tab whose pane the range belongs to.</param>
/// <param name="Pane">The pane the range is drawn in.</param>
/// <param name="Range">The rows it covers.</param>
public sealed record VisualSelection(TabId TabId, PaneKind Pane, RowRange Range);
