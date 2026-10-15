using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// Decides the columns of a graph row from the terminal width, as pure data (like <see cref="PaneLayout"/>). The message
/// column takes all the width the other columns leave, so a wide terminal is used in full; date and hash have fixed
/// widths (the date shows the time too when it fits, else the date alone), the author column is wider on wide rows,
/// and columns are dropped, hash first, then the time, then the date, then author, when the message would get fewer than <see cref="MinimumMessageWidth"/> columns. From
/// <see cref="SpaciousRowWidth"/> columns the gaps between the columns are wider and the row keeps a right margin (#244).
/// </summary>
public static class GraphColumnLayout
{
    /// <summary>Narrowest message column that still lets another column be shown.</summary>
    public const int MinimumMessageWidth = 30;

    /// <summary>Columns of the author column at normal widths; longer names end with an ellipsis.</summary>
    public const int AuthorWidth = 16;

    /// <summary>Author columns when the row is at least <see cref="WideRowWidth"/> wide: full names fit.</summary>
    public const int WideAuthorWidth = 20;

    /// <summary>Row width (after the lanes) from which the author column is wide.</summary>
    public const int WideRowWidth = 140;

    /// <summary>Columns of the date-only column: <c>yyyy-MM-dd</c>, the fallback when the time does not fit.</summary>
    public const int DateOnlyWidth = 10;

    /// <summary>Columns of the date column with the time: <c>yyyy-MM-dd HH:mm</c> (#318).</summary>
    public const int DateWidth = 16;

    /// <summary>Columns of the short hash.</summary>
    public const int HashWidth = 7;

    /// <summary>Row width (after the lanes) from which the columns get roomy gaps and a right margin (#244).</summary>
    public const int SpaciousRowWidth = 100;

    /// <summary>Blank columns before each column after the message on compact rows.</summary>
    public const int CompactColumnGap = 1;

    /// <summary>Blank columns before each column after the message on spacious rows.</summary>
    public const int SpaciousColumnGap = 3;

    /// <summary>Blank columns after the last column on spacious rows; compact rows have none.</summary>
    public const int SpaciousRightMargin = 2;

    /// <summary>Columns of a row of <paramref name="width"/> whose lane area has <paramref name="laneColumns"/> lanes (two columns each).</summary>
    public static GraphColumns Compute(int width, int laneColumns)
    {
        var laneWidth = laneColumns * 2;
        var available = Math.Max(0, width - laneWidth - 1);
        var authorWidth = available >= WideRowWidth ? WideAuthorWidth : AuthorWidth;
        var isSpacious = available >= SpaciousRowWidth;
        var gap = isSpacious ? SpaciousColumnGap : CompactColumnGap;
        var margin = isSpacious ? SpaciousRightMargin : 0;
        var fixedColumns = margin;
        var author = 0;
        var date = 0;
        var hash = 0;

        if (Fits(available, fixedColumns + gap + authorWidth))
        {
            author = authorWidth;
            fixedColumns += gap + authorWidth;
            var dateWidth = Fits(available, fixedColumns + gap + DateWidth) ? DateWidth : DateOnlyWidth;
            if (Fits(available, fixedColumns + gap + dateWidth))
            {
                date = dateWidth;
                fixedColumns += gap + dateWidth;
                if (Fits(available, fixedColumns + gap + HashWidth))
                {
                    hash = HashWidth;
                    fixedColumns += gap + HashWidth;
                }
            }
        }

        return new GraphColumns(laneWidth, available - fixedColumns, author, date, hash, gap, margin);
    }

    private static bool Fits(int available, int fixedColumns) => available - fixedColumns >= MinimumMessageWidth;
}
