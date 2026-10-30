using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views.Theme;

/// <summary>Draws a <see cref="StyledLine"/> into a view at a position.</summary>
public static class LinePainter
{
    public static void Paint(View view, StyledLine line, int column, int row, bool isFocused, bool isRange = false)
    {
        view.Move(column, row);
        foreach (var segment in line.Segments)
        {
            var highlight = HighlightOf(segment, line, isFocused, isRange);
            view.SetAttribute(Palette.For(segment, highlight));
            view.AddStr(segment.Text);
        }
    }

    // A segment can override its line (two columns on one line, only one of them has the selection).
    private static SegmentHighlight HighlightOf(StyledSegment segment, StyledLine line, bool isFocused, bool isRange) =>
        segment.Selection switch
        {
            SegmentSelection.None => new SegmentHighlight(false, isFocused),
            SegmentSelection.Focused => new SegmentHighlight(true, true),
            SegmentSelection.Dim => new SegmentHighlight(true, false),
            SegmentSelection.Range => new SegmentHighlight(true, isFocused, true),
            _ => new SegmentHighlight(line.IsSelected, isFocused, isRange && line.IsSelected),
        };
}
