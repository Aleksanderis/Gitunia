namespace Gitunia.Tui.Rendering;

/// <summary>The blank columns between a pane's frame and its text, so text does not hug the lines.</summary>
public static class PanePadding
{
    /// <summary>Blank columns on each side of a pane's text.</summary>
    public const int Columns = 1;

    /// <summary>Columns left for text in a pane of <paramref name="paneWidth"/> columns.</summary>
    public static int TextWidth(int paneWidth) => Math.Max(0, paneWidth - 2 * Columns);

    /// <summary>The line with the padding on both sides; the padding is selected with the line.</summary>
    public static StyledLine Wrap(StyledLine line)
    {
        var padding = new StyledSegment(new string(' ', Columns), SegmentRole.Plain);
        return new StyledLine([padding, .. line.Segments, padding], line.IsSelected);
    }
}
