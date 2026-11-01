using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Rebase;

/// <summary>What a rebase onto a ref or commit acts on, as the user saw it when the key was pressed.</summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Name">The branch or tag name of the sidebar row, null when the graph is the target.</param>
/// <param name="CommitHash">The commit of the row or of the graph selection, null when there is none.</param>
public sealed record RebaseSelection(SidebarRowKind? RowKind, string? Name, string? CommitHash);
