namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// The <c>...</c> marker at the right edge of the selected graph or sidebar row (#479):
/// the mouse path to the row's actions menu. Every row of the pane keeps the marker's columns free
/// (the pane padding plus <see cref="ReservedColumns"/>), so the marker never covers text.
/// </summary>
public static class RowMenuMarker
{
    public const string Text = "...";

    /// <summary>Panes narrower than this show no marker (the keyboard path stays).</summary>
    public const int MinimumPaneWidth = 20;

    public static int Width => Text.Length;

    public static bool IsShown(int paneWidth) => paneWidth >= MinimumPaneWidth;

    /// <summary>Columns every row leaves blank besides the right padding of <paramref name="paddingColumns"/>.</summary>
    public static int ReservedColumns(int paneWidth, int paddingColumns) =>
        IsShown(paneWidth) ? Math.Max(0, Width - paddingColumns) : 0;

    /// <summary>The line followed by <paramref name="reserved"/> blank columns.</summary>
    public static StyledLine WithReserve(StyledLine line, int reserved) =>
        reserved <= 0
            ? line
            : line with { Segments = [.. line.Segments, new StyledSegment(new string(' ', reserved), SegmentRole.Plain)] };

    /// <summary>True when a click at <paramref name="column"/> of a pane row hits the marker of the selected row.</summary>
    public static bool IsHit(int paneWidth, int column, bool isSelectedRow) =>
        isSelectedRow && IsShown(paneWidth) && column >= paneWidth - Width && column < paneWidth;

    /// <summary>
    /// Replaces the last columns of the cursor row's full-width line (blank by construction) with the marker;
    /// other lines are returned as they are.
    /// </summary>
    public static StyledLine Apply(StyledLine line, int width, bool isCursor)
    {
        if (!isCursor || !IsShown(width))
        {
            return line;
        }

        var keep = width - Width;
        var segments = new List<StyledSegment>();
        var used = 0;
        foreach (var segment in line.Segments)
        {
            if (used >= keep)
            {
                break;
            }

            var segmentWidth = TextWidth.Of(segment.Text);
            if (used + segmentWidth <= keep)
            {
                segments.Add(segment);
                used += segmentWidth;
                continue;
            }

            var cut = TextWidth.Fit(segment.Text, keep - used, string.Empty);
            if (cut.Length > 0)
            {
                segments.Add(segment with { Text = cut });
            }

            used += TextWidth.Of(cut);
        }

        if (used < keep)
        {
            segments.Add(new StyledSegment(new string(' ', keep - used), SegmentRole.Plain));
        }

        segments.Add(new StyledSegment(Text, SegmentRole.Faint));
        return line with { Segments = segments };
    }
}
