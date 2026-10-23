using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>The parts of a tab's pane layout the user changes: whether the diff pane is open, which pane is zoomed and the sizes they dragged.</summary>
/// <param name="IsDiffOpen">The diff pane is open.</param>
/// <param name="Zoomed">The pane that fills the screen, null for the normal layout.</param>
/// <param name="Sizes">The pane sizes the user chose, null for all automatic.</param>
/// <param name="ZoomBoth">With a zoomed detail or diff pane: detail and diff fill the area together (#632).</param>
public sealed record PaneArrangement(bool IsDiffOpen, PaneKind? Zoomed, PaneSizes? Sizes = null, bool ZoomBoth = false)
{
    public static PaneArrangement Normal { get; } = new(false, null);
}
