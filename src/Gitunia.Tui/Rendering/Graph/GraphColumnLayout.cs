namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// Decides the columns of a graph row from the terminal width, as pure data (like <see cref="PaneLayout"/>). The message
/// column takes all the width the other columns leave, so a wide terminal is used in full; date and hash have fixed
/// widths, the author column is wider on wide rows, and columns are dropped, hash first, then
/// date, then author, when the message would get fewer than <see cref="MinimumMessageWidth"/> columns.
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

    /// <summary>Columns of the date column: <c>yyyy-MM-dd</c>.</summary>
    public const int DateWidth = 10;

    /// <summary>Columns of the short hash.</summary>
    public const int HashWidth = 7;

    /// <summary>Columns of a row of <paramref name="width"/> whose lane area has <paramref name="laneColumns"/> lanes (two columns each).</summary>
    public static GraphColumns Compute(int width, int laneColumns)
    {
        var laneWidth = laneColumns * 2;
        var available = Math.Max(0, width - laneWidth - 1);
        var authorWidth = available >= WideRowWidth ? WideAuthorWidth : AuthorWidth;
        var fixedColumns = 0;
        var author = 0;
        var date = 0;
        var hash = 0;

        if (Fits(available, fixedColumns + 1 + authorWidth))
        {
            author = authorWidth;
            fixedColumns += 1 + authorWidth;
            if (Fits(available, fixedColumns + 1 + DateWidth))
            {
                date = DateWidth;
                fixedColumns += 1 + DateWidth;
                if (Fits(available, fixedColumns + 1 + HashWidth))
                {
                    hash = HashWidth;
                    fixedColumns += 1 + HashWidth;
                }
            }
        }

        return new GraphColumns(laneWidth, available - fixedColumns, author, date, hash);
    }

    private static bool Fits(int available, int fixedColumns) => available - fixedColumns >= MinimumMessageWidth;
}
