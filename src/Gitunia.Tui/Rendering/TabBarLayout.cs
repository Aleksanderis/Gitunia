namespace Gitunia.Tui.Rendering;

/// <summary>
/// Lays the tabs out in one row: each tab is <c> N name  branch markers </c>. When they do not all fit, the row starts at
/// the first tab that keeps the active one visible and shows <c>&lt;</c> before it when tabs are cut off on the left (a cut on the right ends in an ellipsis).
/// Markers: <c>*</c> dirty, <c>↑n</c> ahead, <c>↓n</c> behind (<c>+n</c>/<c>-n</c> without Unicode), <c>!</c> missing, an ellipsis while loading.
/// </summary>
public static class TabBarLayout
{
    private const int GapColumns = 1;

    /// <summary>The cells visible in a row of <paramref name="width"/> columns, in order, with their start columns.</summary>
    public static IReadOnlyList<TabCell> Layout(IReadOnlyList<TabLabel> labels, int width, GraphGlyphSet glyphs)
    {
        var texts = labels.Select(label => Describe(label, glyphs)).ToList();
        var activeIndex = Math.Max(0, labels.ToList().FindIndex(label => label.IsActive));
        var first = 0;
        while (first < activeIndex && IndicatorWidth(first) + SpanWidth(texts, first, activeIndex) > width)
        {
            first++;
        }

        var cells = new List<TabCell>();
        var column = IndicatorWidth(first);
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
        var text = " " + label.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (label.IsMissing ? "! " : string.Empty) + label.Name;
        if (label.IsMissing)
        {
            return text + " (missing) ";
        }

        if (label.Branch is { } branch)
        {
            text += "  " + branch;
        }

        var markers = string.Empty;
        markers += label.IsDirty ? "*" : string.Empty;
        markers += label.Ahead > 0 ? (glyphs.IsUnicode ? "↑" : "+") + label.Ahead.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        markers += label.Behind > 0 ? (glyphs.IsUnicode ? "↓" : "-") + label.Behind.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        markers += label.IsLoading ? glyphs.Ellipsis : string.Empty;
        return markers.Length > 0 ? text + " " + markers + " " : text + " ";
    }
}
