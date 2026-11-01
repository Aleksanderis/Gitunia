using Gitunia.App.Loading.Trees;
using Gitunia.Core.FileTree;
using Gitunia.Tui.Rendering.Sidebar;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>What the Files tab shows: the load state of the tree and its visible rows.</summary>
/// <param name="State">The tree of the selected commit and its load state.</param>
/// <param name="Rows">The visible rows (empty until the tree is loaded).</param>
/// <param name="Filter">The active filter text, empty for none.</param>
/// <param name="Glyphs">The characters for open and closed directories.</param>
public sealed record FileTreeView(
    CommitTreeState State,
    IReadOnlyList<FileTreeRow> Rows,
    string Filter,
    SidebarGlyphs Glyphs);
