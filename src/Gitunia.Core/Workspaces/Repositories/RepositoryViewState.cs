using Gitunia.Core.Details;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>What a tab remembers about how the user looks at its repository.</summary>
/// <param name="SelectedCommit">The selected commit, tracked by id so it survives refreshes; null selects the newest.</param>
/// <param name="ScrollOffset">Index of the first visible graph row.</param>
/// <param name="FilterText">The active graph filter, empty when none.</param>
/// <param name="FocusedPane">The pane that has focus.</param>
/// <param name="SidebarWidth">Columns of the sidebar the user set, null for automatic. A wish: the pane layout clamps it.</param>
/// <param name="BottomHeight">Rows of the bottom strip the user set, null for automatic.</param>
/// <param name="DetailWidth">Columns of the detail pane beside an open diff the user set, null for automatic.</param>
/// <param name="BottomTab">The tab of the bottom detail pane that is showing.</param>
/// <param name="Mode">The view the tab shows, the history or the working tree. Not saved with the session.</param>
/// <param name="PathStyle">How the file lists show a path (name first or the full path). Not saved with the
/// session.</param>
/// <param name="CompareMark">The commit marked as one end of a comparison (#535), null when none. Not saved with the
/// session.</param>
/// <param name="FileTree">The filter and the opened directories of the Files tab and the closed folders of the Changes
/// tab's tree style, null for none. Not saved with the session; read it through <see cref="DetailTree"/>.</param>
/// <param name="Diff">What the diff pane shows and how it is laid out, null for a pane never used. Not saved with the
/// session; read it through <see cref="DiffPane"/>.</param>
public sealed record RepositoryViewState(
    ObjectId? SelectedCommit,
    int ScrollOffset,
    string FilterText,
    PaneKind FocusedPane,
    int? SidebarWidth = null,
    int? BottomHeight = null,
    int? DetailWidth = null,
    BottomTab BottomTab = BottomTab.Changes,
    TabViewMode Mode = TabViewMode.History,
    FilePathStyle PathStyle = FilePathStyle.NameFirst,
    ObjectId? CompareMark = null,
    DetailTreeState? FileTree = null,
    DiffPaneState? Diff = null)
{
    /// <summary>The tab's file tree state, empty when none was set.</summary>
    public DetailTreeState DetailTree => FileTree ?? DetailTreeState.Empty;

    /// <summary>The tab's diff pane state, empty when none was set.</summary>
    public DiffPaneState DiffPane => Diff ?? DiffPaneState.Empty;

    /// <summary>A fresh view: nothing selected, top of the graph, no filter, graph focused.</summary>
    public static RepositoryViewState Initial { get; } = new(null, 0, string.Empty, PaneKind.Graph);
}
