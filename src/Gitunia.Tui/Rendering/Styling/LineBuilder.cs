namespace Gitunia.Tui.Rendering.Styling;

/// <summary>Builds one line of an exact width: appended text is cut at the right edge and the rest is padded with spaces.</summary>
public sealed class LineBuilder(int width, string ellipsis)
{
    private readonly List<StyledSegment> _segments = [];
    private int _used;

    /// <summary>Columns still free.</summary>
    public int Remaining => Math.Max(0, width - _used);

    /// <summary>Adds text, shortened to the remaining columns.</summary>
    public LineBuilder Append(string text, SegmentRole role, int colorIndex = 0)
    {
        if (Remaining == 0 || text.Length == 0)
        {
            return this;
        }

        var fitted = TextWidth.Fit(text, Remaining, ellipsis);
        if (fitted.Length > 0)
        {
            _segments.Add(new StyledSegment(fitted, role, colorIndex));
            _used += TextWidth.Of(fitted);
        }

        return this;
    }

    /// <summary>Adds a finished line (one column of a table row) as it is; it must not be wider than the remaining columns.</summary>
    public LineBuilder Append(StyledLine line)
    {
        foreach (var segment in line.Segments)
        {
            _segments.Add(segment);
            _used += TextWidth.Of(segment.Text);
        }

        return this;
    }

    /// <summary>Fills the rest of the line with <paramref name="fill"/> (a one-column character) as a <see cref="SegmentRole.Rule"/> stretch of the frame's edge and returns it.</summary>
    public StyledLine BuildRule(char fill)
    {
        if (Remaining > 0)
        {
            _segments.Add(new StyledSegment(new string(fill, Remaining), SegmentRole.Rule));
            _used = width;
        }

        return new StyledLine(_segments);
    }

    /// <summary>Pads the rest of the line with spaces of <paramref name="fillRole"/> and returns it.</summary>
    public StyledLine Build(SegmentRole fillRole = SegmentRole.Plain, bool isSelected = false)
    {
        if (Remaining > 0)
        {
            _segments.Add(new StyledSegment(new string(' ', Remaining), fillRole));
            _used = width;
        }

        return new StyledLine(_segments, isSelected);
    }
}
