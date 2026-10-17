using Gitunia.Core.Sidebar;

namespace Gitunia.Tui.App.Pick;

/// <summary>
/// What a cherry-pick or revert acts on: the commits of the graph selection (a single commit, or the Visual range,
/// newest first as the graph shows them), or the commit of the selected sidebar branch or tag.
/// </summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Name">The branch or tag name of the sidebar row, null when the graph is the target.</param>
/// <param name="Hashes">The commit hashes, in graph order (newest first); empty when nothing is selected.</param>
public sealed record PickSelection(SidebarRowKind? RowKind, string? Name, IReadOnlyList<string> Hashes);
