using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// The look of a <see cref="Gitunia.Core.Filtering.FilterList{T}"/> in an overlay or popup, so every filter surface draws
/// it the same way: the title bar with the position (<see cref="Gitunia.Core.Text.PositionText"/>) at its right end, the
/// rows with the selection bar, and the empty, loading or failure text in the empty-text role. It replaces the
/// hand-built "title with count" rows of the prompt popup and the repository picker.
/// </summary>
public static class FilterListFrame
{
    private const int MinimumTitleGap = 2;
    private const int FieldRoom = 4;

    /// <summary>
    /// The title bar: <paramref name="title"/>, the field being edited after it (null for none) and
    /// <paramref name="right"/> (the position, or a count) right-aligned; the right text is left out when the title and
    /// field would not fit beside it.
    /// </summary>
    public static StyledLine Title(string title, CaretField? field, string? right, int width, GraphGlyphSet glyphs)
    {
        var left = $" {title}";
        var rightText = string.IsNullOrEmpty(right) ? string.Empty : $"{right} ";
        var leftWidth = width - TextWidth.Of(rightText) - MinimumTitleGap;
        var neededWidth = TextWidth.Of(left) + (field is null ? 0 : TextWidth.Of(field.Prefix) + FieldRoom);
        if (rightText.Length == 0 || leftWidth < neededWidth)
        {
            return Single(left, field, width, glyphs);
        }

        var gap = width - leftWidth - TextWidth.Of(rightText);
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append(Single(left, field, leftWidth, glyphs))
            .Append(new string(' ', gap), SegmentRole.OverlayTitle)
            .Append(rightText, SegmentRole.OverlayTitle);

        return builder.Build(SegmentRole.OverlayTitle);
    }

    /// <summary>The rows of <paramref name="window"/> as body lines, or its empty text alone when there are no rows.</summary>
    public static IEnumerable<StyledLine> Rows(PickerWindow window, int width, GraphGlyphSet glyphs)
    {
        if (window.EmptyText is { } empty && window.Rows.Count == 0)
        {
            return [EmptyRow(empty, width, glyphs)];
        }

        return window.Rows.Select((row, index) => OverlayBox.Text(row, width, glyphs, isSelected: index == window.Highlight));
    }

    /// <summary>A body row with what the list says instead of rows, in the empty-text role (never a normal row).</summary>
    public static StyledLine EmptyRow(string text, int width, GraphGlyphSet glyphs) =>
        OverlayBox.Text(text, width, glyphs, SegmentRole.EmptyText);

    private static StyledLine Single(string left, CaretField? field, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append(left, SegmentRole.OverlayTitle);
        field?.AppendTo(builder, FieldRoles.Title);

        return builder.Build(SegmentRole.OverlayTitle);
    }
}
