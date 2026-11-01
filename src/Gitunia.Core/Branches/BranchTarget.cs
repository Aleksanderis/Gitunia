using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Branches;

/// <summary>What a branch action acts on, as the user saw it when pressing the key: a sidebar row or the graph
/// commit.</summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Name">The branch name (or folder path) of the sidebar row, null when the graph is the target.</param>
/// <param name="CommitHash">The commit of the row or of the graph selection, null when there is none.</param>
public sealed record BranchTarget(SidebarRowKind? RowKind, string? Name, string? CommitHash);
