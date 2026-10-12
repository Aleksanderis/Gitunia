using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.Rendering;

/// <summary>The parts of a tab's pane layout the user changes: whether the diff pane is open and which pane is zoomed.</summary>
/// <param name="IsDiffOpen">The diff pane is open.</param>
/// <param name="Zoomed">The pane that fills the screen, null for the normal layout.</param>
public sealed record PaneArrangement(bool IsDiffOpen, PaneKind? Zoomed)
{
    public static PaneArrangement Normal { get; } = new(false, null);
}
