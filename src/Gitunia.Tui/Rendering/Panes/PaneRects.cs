namespace Gitunia.Tui.Rendering.Panes;

/// <summary>Where every piece of the repository screen sits, as <see cref="PaneLayout"/> computed it.</summary>
/// <param name="Sidebar">Rows of the sidebar list.</param>
/// <param name="SidebarFilter">The sidebar's filter row below its list.</param>
/// <param name="SidebarDivider">The line between the sidebar and the graph.</param>
/// <param name="Graph">The commit graph list.</param>
/// <param name="DetailTitle">The title row of the detail pane.</param>
/// <param name="Detail">The rows of the detail pane.</param>
/// <param name="BottomDivider">The line between the detail pane and the diff pane when they sit side by side.</param>
/// <param name="DiffTitle">The title row of the diff pane.</param>
/// <param name="Diff">The rows of the diff pane.</param>
public sealed record PaneRects(
    PaneRect Sidebar,
    PaneRect SidebarFilter,
    PaneRect SidebarDivider,
    PaneRect Graph,
    PaneRect DetailTitle,
    PaneRect Detail,
    PaneRect BottomDivider,
    PaneRect DiffTitle,
    PaneRect Diff);
