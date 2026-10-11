namespace Gitunia.Tui.Rendering;

/// <summary>Builds one line of an exact width: appended text is cut at the right edge and the rest is padded with spaces.</summary>
public sealed class LineBuilder(int width, string ellipsis)
{
    private readonly List<StyledSegment> segments = [];
    private int used;

    /// <summary>Columns still free.</summary>
    public int Remaining => Math.Max(0, width - used);

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
            segments.Add(new StyledSegment(fitted, role, colorIndex));
            used += TextWidth.Of(fitted);
        }

        return this;
    }

    /// <summary>Pads the rest of the line with spaces of <paramref name="fillRole"/> and returns it.</summary>
    public StyledLine Build(SegmentRole fillRole = SegmentRole.Plain, bool isSelected = false)
    {
        if (Remaining > 0)
        {
            segments.Add(new StyledSegment(new string(' ', Remaining), fillRole));
            used = width;
        }

        return new StyledLine(segments, isSelected);
    }
}
