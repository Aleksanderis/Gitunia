using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Remotes;

/// <summary>What a remote action acts on, as the user saw it when pressing the key: a selected sidebar row or nothing
/// (then the remote is chosen in a picker).</summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Name">The name of the sidebar row (a remote branch is written <c>remote/branch</c>), null when none.</param>
public sealed record RemoteTarget(SidebarRowKind? RowKind, string? Name);
