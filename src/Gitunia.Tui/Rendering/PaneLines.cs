namespace Gitunia.Tui.Rendering;

/// <summary>A pane's lines as <see cref="ScreenComposer"/> places them on the screen.</summary>
/// <param name="Left">Column of the first character.</param>
/// <param name="Top">Row of the first line.</param>
/// <param name="Lines">The lines, top to bottom.</param>
/// <param name="Selection">How the selected rows of the pane are tinted (focused or dim).</param>
internal sealed record PaneLines(int Left, int Top, IReadOnlyList<StyledLine> Lines, SegmentSelection Selection);
