using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>The blank columns between a pane's frame and its text, so text does not hug the lines.</summary>
public static class PanePadding
{
    /// <summary>Blank columns on each side of a pane's text on compact screens.</summary>
    public const int Columns = 1;

    /// <summary>Blank columns on each side of the text of the history and the bottom panes on spacious screens (#244).</summary>
    public const int SpaciousColumns = 2;

    /// <summary>Screen width from which the history and the bottom panes use <see cref="SpaciousColumns"/> (the graph's own threshold).</summary>
    public const int SpaciousScreenWidth = 100;

    /// <summary>Blank columns left of the graph rows: none, so the first lane sits next to the frame (#752).</summary>
    public const int GraphLeftColumns = 0;

    /// <summary>Padding columns of the history and the bottom panes on a screen of <paramref name="screenWidth"/> columns.</summary>
    public static int ColumnsFor(int screenWidth) => screenWidth >= SpaciousScreenWidth ? SpaciousColumns : Columns;

    /// <summary>Columns left for text in a pane of <paramref name="paneWidth"/> columns.</summary>
    public static int TextWidth(int paneWidth, int columns = Columns) => Math.Max(0, paneWidth - 2 * columns);

    /// <summary>Columns left for graph rows in a pane of <paramref name="paneWidth"/> columns (no left pad).</summary>
    public static int GraphTextWidth(int paneWidth, int columns = Columns) =>
        Math.Max(0, paneWidth - columns - GraphLeftColumns);

    /// <summary>A graph row with the right padding only: the first lane starts at the pane edge (#752).</summary>
    public static StyledLine WrapGraph(StyledLine line, int columns = Columns)
    {
        var padding = new StyledSegment(new string(' ', columns), SegmentRole.Plain);
        return new StyledLine([.. line.Segments, padding], line.IsSelected);
    }

    /// <summary>The line with the padding on both sides; the padding is selected with the line.</summary>
    public static StyledLine Wrap(StyledLine line, int columns = Columns)
    {
        var padding = new StyledSegment(new string(' ', columns), SegmentRole.Plain);
        return new StyledLine([padding, .. line.Segments, padding], line.IsSelected);
    }
}
