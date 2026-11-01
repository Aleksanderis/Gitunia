using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Sync;

/// <summary>What a remote branch delete acts on, as the user saw it when pressing the key: a selected sidebar row or the
/// selected graph commit.</summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Name">The name of the sidebar row (a remote branch is written <c>remote/branch</c>), null when none.</param>
/// <param name="CommitHash">The selected commit, null when none.</param>
public sealed record SyncTarget(SidebarRowKind? RowKind, string? Name, string? CommitHash);
