using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Tags;

/// <summary>What a tag action acts on, as the user saw it when pressing the key: a sidebar row or the graph
/// commit.</summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Name">The branch or tag name of the sidebar row, null when the graph is the target.</param>
/// <param name="CommitHash">The commit of the row or of the graph selection, null when there is none.</param>
public sealed record TagTarget(SidebarRowKind? RowKind, string? Name, string? CommitHash);
