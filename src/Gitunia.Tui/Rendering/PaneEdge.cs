namespace Gitunia.Tui.Rendering;

/// <summary>A divider the user can move.</summary>
public enum PaneEdge
{
    /// <summary>The vertical line between the sidebar and the graph.</summary>
    Sidebar,

    /// <summary>The horizontal edge between the graph and the bottom strip (the title row of the detail and diff panes).</summary>
    Bottom,

    /// <summary>The vertical line between the detail pane and the diff pane.</summary>
    Detail,
}
