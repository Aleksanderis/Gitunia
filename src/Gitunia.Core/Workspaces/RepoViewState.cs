using Gitunia.Git;

namespace Gitunia.Core.Workspaces;

/// <summary>What a tab remembers about how the user looks at its repository.</summary>
/// <param name="SelectedCommit">The selected commit, tracked by id so it survives refreshes; null selects the newest.</param>
/// <param name="ScrollOffset">Index of the first visible graph row.</param>
/// <param name="FilterText">The active graph filter, empty when none.</param>
/// <param name="FocusedPane">The pane that has focus.</param>
/// <param name="SidebarWidth">Columns of the sidebar the user set, null for automatic. A wish: the pane layout clamps it.</param>
/// <param name="BottomHeight">Rows of the bottom strip the user set, null for automatic.</param>
/// <param name="DetailWidth">Columns of the detail pane beside an open diff the user set, null for automatic.</param>
public sealed record RepoViewState(
    ObjectId? SelectedCommit,
    int ScrollOffset,
    string FilterText,
    PaneKind FocusedPane,
    int? SidebarWidth = null,
    int? BottomHeight = null,
    int? DetailWidth = null)
{
    /// <summary>A fresh view: nothing selected, top of the graph, no filter, graph focused.</summary>
    public static RepoViewState Initial { get; } = new(null, 0, string.Empty, PaneKind.Graph);
}
