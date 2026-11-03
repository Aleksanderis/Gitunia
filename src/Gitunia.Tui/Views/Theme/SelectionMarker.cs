using System.Text;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Theme;

/// <summary>Marks a stretch of screen columns of a line as part of a character-wise selection (the segments are split where it starts and ends).</summary>
public static class SelectionMarker
{
    /// <summary>The line with the columns <paramref name="from"/> up to (not including) <paramref name="to"/> in the selection tint.</summary>
    /// <param name="line">The line.</param>
    /// <param name="from">The first column.</param>
    /// <param name="to">The column after the last one.</param>
    public static StyledLine Mark(StyledLine line, int from, int to)
    {
        if (to <= from)
        {
            return line;
        }

        var segments = new List<StyledSegment>();
        var column = 0;
        foreach (var segment in line.Segments)
        {
            column = Split(segment, column, from, to, segments);
        }

        return line with { Segments = segments };
    }

    // Appends the pieces of the segment before, inside and after the stretch; returns the column after the segment.
    private static int Split(StyledSegment segment, int start, int from, int to, List<StyledSegment> output)
    {
        var before = new StringBuilder();
        var inside = new StringBuilder();
        var after = new StringBuilder();
        var column = start;
        var counter = new WidthCounter();
        foreach (var rune in segment.Text.EnumerateRunes())
        {
            var width = counter.Next(rune);
            var isInside = column < to && column + Math.Max(width, 1) > from;
            var target = after;
            if (isInside)
            {
                target = inside;
            }
            else if (column < from)
            {
                target = before;
            }

            target.Append(rune.ToString());
            column += width;
        }

        Add(output, segment, before, segment.Selection);
        Add(output, segment, inside, SegmentSelection.Range);
        Add(output, segment, after, segment.Selection);

        return column;
    }

    private static void Add(List<StyledSegment> output, StyledSegment segment, StringBuilder text, SegmentSelection selection)
    {
        if (text.Length > 0)
        {
            output.Add(segment with { Text = text.ToString(), Selection = selection });
        }
    }
}
