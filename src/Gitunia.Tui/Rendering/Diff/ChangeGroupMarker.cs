using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>Marks a padded diff line as part of the change group under the cursor: a bar in the left margin and the group tint.</summary>
public static class ChangeGroupMarker
{
    /// <param name="line">The line with its padding (<see cref="Panes.PanePadding.Wrap"/>), whose first segment is the left margin.</param>
    /// <param name="glyphs">Supplies the bar.</param>
    public static StyledLine Mark(StyledLine line, GraphGlyphSet glyphs)
    {
        if (line.Segments.Count == 0)
        {
            return line;
        }

        var segments = new List<StyledSegment>(line.Segments.Count + 1);
        var margin = line.Segments[0];
        var bar = glyphs.For(LineDirections.Up | LineDirections.Down).ToString();
        segments.Add(new StyledSegment(bar, SegmentRole.DiffGroupBar, InGroup: true));
        if (margin.Text.Length > 1)
        {
            segments.Add(margin with { Text = margin.Text[1..], InGroup = true });
        }

        for (var index = 1; index < line.Segments.Count; index++)
        {
            segments.Add(line.Segments[index] with { InGroup = true });
        }

        return line with { Segments = segments };
    }
}
