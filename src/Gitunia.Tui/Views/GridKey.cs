using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.Views;

/// <summary>Everything the frame grid depends on, so it is rebuilt only when one of them changes.</summary>
/// <param name="Width">Screen columns.</param>
/// <param name="Height">Screen rows.</param>
/// <param name="Rects">Pane rectangles.</param>
/// <param name="Focused">The focused pane.</param>
/// <param name="Title">The graph title.</param>
/// <param name="IsPicker">The repository picker is shown instead of the panes.</param>
/// <param name="IsAscii">ASCII glyphs are in use.</param>
public readonly record struct GridKey(
    int Width,
    int Height,
    PaneRects Rects,
    PaneKind? Focused,
    string Title,
    bool IsPicker,
    bool IsAscii);
