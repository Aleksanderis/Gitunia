namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// The <c>...</c> marker at the right edge of the selected graph or sidebar row (#479): the mouse path to the row's actions menu.
/// Rendering puts it there, the pane treats a click on those columns as a button click.
/// </summary>
public static class RowMenuMarker
{
    public const string Text = "...";

    /// <summary>Panes narrower than this show no marker (the keyboard path stays).</summary>
    public const int MinimumPaneWidth = 20;

    public static int Width => Text.Length;

    public static bool IsShown(int paneWidth) => paneWidth >= MinimumPaneWidth;

    /// <summary>Replaces the last columns of the cursor row's full-width line with the marker; other lines are returned as they are.</summary>
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
