using System.Collections.Immutable;
using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Pick;

/// <summary>
/// What a cherry-pick or revert acts on, as the user saw it when the key was pressed: the commits of the graph
/// selection (a single commit, or the Visual range, newest first as the graph shows them), or the commit of the
/// selected sidebar branch or tag.
/// </summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Hashes">The commit hashes, in graph order (newest first); empty when nothing is selected.</param>
public sealed record PickTarget(SidebarRowKind? RowKind, ImmutableArray<string> Hashes);
