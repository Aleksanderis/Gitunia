using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>One box of the frame: a pane's content rectangle, whether it has focus and the title on its top edge.</summary>
/// <param name="Content">The pane's content rectangle; its box is drawn one cell outside it.</param>
/// <param name="IsFocused">The pane has focus, so its edges are drawn in the accent colour.</param>
/// <param name="Title">Title on the top edge, empty for none.</param>
public sealed record FrameBox(PaneRect Content, bool IsFocused, string Title);
