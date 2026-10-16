using Gitunia.Core.Sidebar;

namespace Gitunia.Tui.App.Branches;

/// <summary>What a branch action acts on: the selected sidebar row, or the commit selected in the graph.</summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Name">The branch or tag name of the sidebar row, null when the graph is the target.</param>
/// <param name="CommitHash">The commit of the row or of the graph selection, null when there is none.</param>
public sealed record BranchSelection(SidebarRowKind? RowKind, string? Name, string? CommitHash);
