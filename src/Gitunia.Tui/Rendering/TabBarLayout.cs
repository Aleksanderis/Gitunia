namespace Gitunia.Tui.Rendering;

/// <summary>
/// Lays the tabs out in one row on the bar: a margin, then each tab as a padded chip <c>  N name markers  </c> (the active one is the
/// accent chip, the others are muted text on the bar), one blank column between tabs (#220); the branch only when the label has one
/// (off by default, #219). At the right end sits the <see cref="CommandsLabel"/> button when the row is wide enough. When the tabs do not all fit, the row starts at
/// the first tab that keeps the active one visible and shows <c>&lt;</c> before it when tabs are cut off on the left (a cut on the right ends in an ellipsis).
/// Markers: <c>*</c> dirty, <c>↑n</c> ahead, <c>↓n</c> behind (<c>+n</c>/<c>-n</c> without Unicode), <c>!</c> missing, an ellipsis while loading.
/// </summary>
public static class TabBarLayout
{
    /// <summary>The button at the right end of the row that opens the command palette, the mouse path of <c>:</c> (#221).</summary>
    public const string CommandsLabel = " : commands ";

    /// <summary>Rows narrower than this have no commands button.</summary>
    public const int MinimumWidthForCommands = 44;

    /// <summary>Blank columns before the first tab.</summary>
    public const int LeftMargin = 1;

    private const int GapColumns = 1;
    private const int Padding = 2;

    /// <summary>The first column of the commands button in a row of <paramref name="width"/> columns, or null when the row has none.</summary>
    public static int? CommandsStart(int width) => width >= MinimumWidthForCommands ? width - CommandsLabel.Length - 1 : null;

    /// <summary>Whether <paramref name="column"/> is on the commands button of a row of <paramref name="width"/> columns.</summary>
    public static bool IsOnCommands(int width, int column) => CommandsStart(width) is { } start && column >= start && column < start + CommandsLabel.Length;

    /// <summary>The cells visible in a row of <paramref name="width"/> columns, in order, with their start columns.</summary>
    public static IReadOnlyList<TabCell> Layout(IReadOnlyList<TabLabel> labels, int width, GraphGlyphSet glyphs)
    {
        var available = (CommandsStart(width) ?? width) - GapColumns;
        var texts = labels.Select(label => Describe(label, glyphs)).ToList();
        var activeIndex = Math.Max(0, labels.ToList().FindIndex(label => label.IsActive));
        var first = 0;
        while (first < activeIndex && LeftMargin + IndicatorWidth(first) + SpanWidth(texts, first, activeIndex) > available)
        {
            first++;
        }

        var cells = new List<TabCell>();
        var column = LeftMargin + IndicatorWidth(first);
        for (var index = first; index < labels.Count; index++)
        {
            cells.Add(new TabCell(labels[index].Number, column, texts[index], labels[index].IsActive));
            column += TextWidth.Of(texts[index]) + GapColumns;
        }

        return cells;
    }

    /// <summary>The tab number at a column of the row, or null between tabs.</summary>
    public static int? NumberAt(IReadOnlyList<TabCell> cells, int column)
    {
        foreach (var cell in cells)
        {
            if (column >= cell.Start && column < cell.Start + cell.Width)
            {
                return cell.Number;
            }
        }

        return null;
    }

    private static int IndicatorWidth(int first) => first > 0 ? 2 : 0;

    private static int SpanWidth(List<string> texts, int first, int last)
    {
        var width = 0;
        for (var index = first; index <= last; index++)
        {
            width += TextWidth.Of(texts[index]) + (index > first ? GapColumns : 0);
        }

        return width;
    }

    private static string Describe(TabLabel label, GraphGlyphSet glyphs)
    {
        var padding = new string(' ', Padding);
        var text = padding + label.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (label.IsMissing ? "! " : string.Empty) + label.Name;
        if (label.IsMissing)
        {
            return text + " (missing)" + padding;
        }

        if (label.Branch is { } branch)
        {
            text += "  " + branch;
        }

        var markers = string.Empty;
        markers += label.IsDirty ? "*" : string.Empty;
        markers += label.Ahead > 0 ? (!glyphs.IsAscii ? "↑" : "+") + label.Ahead.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        markers += label.Behind > 0 ? (!glyphs.IsAscii ? "↓" : "-") + label.Behind.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        markers += label.IsLoading ? glyphs.Ellipsis : string.Empty;
        return markers.Length > 0 ? text + " " + markers + padding : text + padding;
    }
}
