namespace Gitunia.Tui.Rendering;

/// <summary>
/// Decides the columns of a graph row from the terminal width, as pure data (like <see cref="PaneLayout"/>). The message
/// column flexes up to <see cref="MaximumMessageWidth"/>; author, date and hash have fixed widths and are dropped, hash
/// first, then date, then author, when the message would get fewer than <see cref="MinimumMessageWidth"/> columns.
/// </summary>
public static class GraphColumnLayout
{
    /// <summary>Widest the message column grows, so the other columns stay near it on wide terminals.</summary>
    public const int MaximumMessageWidth = 100;

    /// <summary>Narrowest message column that still lets another column be shown.</summary>
    public const int MinimumMessageWidth = 30;

    /// <summary>Columns of the author column; longer names end with an ellipsis.</summary>
    public const int AuthorWidth = 16;

    /// <summary>Columns of the date column: <c>yyyy-MM-dd</c>.</summary>
    public const int DateWidth = 10;

    /// <summary>Columns of the short hash.</summary>
    public const int HashWidth = 7;

    /// <summary>Columns of a row of <paramref name="width"/> whose lane area has <paramref name="laneColumns"/> lanes (two columns each).</summary>
    public static GraphColumns Compute(int width, int laneColumns)
    {
        var laneWidth = laneColumns * 2;
        var available = Math.Max(0, width - laneWidth - 1);
        var fixedColumns = 0;
        var author = 0;
        var date = 0;
        var hash = 0;

        if (Fits(available, fixedColumns + 1 + AuthorWidth))
        {
            author = AuthorWidth;
            fixedColumns += 1 + AuthorWidth;
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

        return new GraphColumns(laneWidth, Math.Min(MaximumMessageWidth, available - fixedColumns), author, date, hash);
    }

    private static bool Fits(int available, int fixedColumns) => Math.Min(MaximumMessageWidth, available - fixedColumns) >= MinimumMessageWidth;
}
