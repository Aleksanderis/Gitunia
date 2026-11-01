using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;

namespace Gitunia.Core.Compare;

/// <summary>What the comparison reducer reads of the tab.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Mark">The marked commit, null when none.</param>
/// <param name="FocusedPane">The focused pane.</param>
public sealed record CompareContext(TabId TabId, ObjectId? Mark, PaneKind FocusedPane);
