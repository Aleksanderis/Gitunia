namespace Gitunia.Tui.Rendering;

/// <summary>
/// Pane sizes the user changed (by dragging a divider or with the resize keys); null means "automatic", the size
/// <see cref="PaneLayout"/> picks from the screen. The values are wishes: the layout clamps them to what fits.
/// </summary>
/// <param name="SidebarWidth">Columns of the sidebar list, without its divider.</param>
/// <param name="BottomHeight">Rows of the bottom strip (detail and diff panes), including the title row.</param>
/// <param name="DetailWidth">Columns of the detail pane beside an open diff.</param>
public sealed record PaneSizes(int? SidebarWidth = null, int? BottomHeight = null, int? DetailWidth = null)
{
    public static PaneSizes Automatic { get; } = new();

    public bool IsAutomatic => this == Automatic;
}
