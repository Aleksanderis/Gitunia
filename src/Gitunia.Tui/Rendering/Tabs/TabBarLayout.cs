using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Tabs;

/// <summary>
/// Lays the tabs out in one row on the bar: a margin, then each tab as a padded chip <c>  N name markers  </c> (the active one is the
/// accent chip, the others are buttons: tinted chips, #240), one blank column between tabs (#220), tabs that do not fit are left out; the branch only when the label has one
/// (off by default, #219). At the right end sits the commands button (<see cref="CommandsChip"/>) when the row is wide enough. When the tabs do not all fit, the row starts at
/// the first tab that keeps the active one visible and shows <c>&lt;</c> before it when tabs are cut off on the left (a cut on the right ends in an ellipsis).
/// Markers: <c>*</c> dirty, <c>↑n</c> ahead, <c>↓n</c> behind (<c>+n</c>/<c>-n</c> without Unicode), <c>!</c> missing, an ellipsis while loading.
/// </summary>
public static class TabBarLayout
{
    /// <summary>The button right after the last tab that opens the repository picker in a new tab, the mouse path of <c>Ctrl-t</c> (#232).</summary>
    public const string NewTabLabel = " + ";

    /// <summary>Rows narrower than this have no commands button.</summary>
    public const int MinimumWidthForCommands = 44;

    /// <summary>Blank columns before the first tab.</summary>
    public const int LeftMargin = 1;

    private const string CommandsText = "commands";
    private const int GapColumns = 1;
    private const int Padding = 2;

    /// <summary>
    /// The button at the right end of the row that opens the command palette, the mouse path of its key (<c>:</c> by
    /// default, #221): the key comes from the keymap (#800), so a rebinding follows; an unbound action shows the label only.
    /// </summary>
    public static TitleButton CommandsChip(ChipKeys? keys = null) =>
        KeyedChip.Keyed(
            DefaultMouseMap.CommandsButton,
            CommandsText,
            (keys ?? ChipKeys.Default).For(DefaultMouseMap.CommandsButton, KeyBindingTable.GlobalView));

    /// <summary>The columns the commands button takes with the key lookup <paramref name="keys"/>.</summary>
    public static int CommandsWidth(ChipKeys? keys = null) => TextWidth.Of(CommandsChip(keys).Label);

    /// <summary>The first column of the commands button in a row of <paramref name="width"/> columns, or null when the row has none.</summary>
    public static int? CommandsStart(int width, ChipKeys? keys = null) =>
        width >= MinimumWidthForCommands ? width - CommandsWidth(keys) - 1 : null;

    /// <summary>Whether <paramref name="column"/> is on the commands button of a row of <paramref name="width"/> columns.</summary>
    public static bool IsOnCommands(int width, int column, ChipKeys? keys = null) =>
        CommandsStart(width, keys) is { } start && column >= start && column < start + CommandsWidth(keys);

    /// <summary>The first column of the new tab button: one blank column after the last tab (or after the margin when there is none).</summary>
    public static int NewTabStart(IReadOnlyList<TabCell> cells) => cells.Count == 0 ? LeftMargin : cells[^1].Start + cells[^1].Width + GapColumns;

    /// <summary>Whether <paramref name="column"/> is on the new tab button that follows <paramref name="cells"/>.</summary>
    public static bool IsOnNewTab(IReadOnlyList<TabCell> cells, int column) => column >= NewTabStart(cells) && column < NewTabStart(cells) + NewTabLabel.Length;

    /// <summary>The cells visible in a row of <paramref name="width"/> columns, in order, with their start columns.</summary>
    public static IReadOnlyList<TabCell> Layout(
        IReadOnlyList<TabLabel> labels,
        int width,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null)
    {
        // The new tab button follows the last tab, so its room is kept free.
        // The remote chips (#456) sit left of the commands button, so they take room from the tabs too.
        var available = (RemoteChips.Start(width, keys) ?? CommandsStart(width, keys) ?? width)
            - GapColumns - NewTabLabel.Length - GapColumns;
        var described = labels.Select(label => Describe(label, glyphs)).ToList();
        var texts = described.Select(item => item.Text).ToList();
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
            // A tab that would reach into the commands button is not shown (the active one always is), so the click zones never overlap.
            if (index > activeIndex && column + TextWidth.Of(texts[index]) > available)
            {
                break;
            }

            // The active tab is cut with an ellipsis when it alone is wider than the row.
            var isCut = column + TextWidth.Of(texts[index]) > available;
            var text = isCut ? TextWidth.Fit(texts[index], Math.Max(0, available - column), glyphs.Ellipsis) : texts[index];
            cells.Add(new TabCell(labels[index].Number, column, text, labels[index].IsActive, isCut ? null : described[index].DirtyOffset));
            column += TextWidth.Of(text) + GapColumns;
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

    /// <summary>The tab number whose dirty marker (<c>*</c>) is at a column of the row, or null when the column is not on one (#331).</summary>
    public static int? DirtyMarkerAt(IReadOnlyList<TabCell> cells, int column)
    {
        foreach (var cell in cells)
        {
            if (cell.DirtyOffset is { } offset && column == cell.Start + offset)
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

    private static TabText Describe(TabLabel label, GraphGlyphSet glyphs)
    {
        var padding = new string(' ', Padding);
        var text = $"{padding}{label.Number.ToString(CultureInfo.InvariantCulture)} {(label.IsMissing ? "! " : string.Empty)}{label.Name}";
        if (label.IsMissing)
        {
            return new TabText($"{text} (missing){padding}", null);
        }

        if (label.Branch is { } branch)
        {
            text = $"{text}  {branch}";
        }

        var markers = string.Empty;
        markers += label.IsDirty ? "*" : string.Empty;
        markers += label.HasOperation ? "!" : string.Empty;
        var aheadGlyph = glyphs.IsAscii ? "+" : "↑";
        var behindGlyph = glyphs.IsAscii ? "-" : "↓";
        markers += label.Ahead > 0 ? $"{aheadGlyph}{label.Ahead.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        var behind = label.Behind.ToString(CultureInfo.InvariantCulture);
        markers += label.Behind > 0 ? $"{behindGlyph}{behind}" : string.Empty;
        markers += label.IsLoading ? glyphs.Ellipsis : string.Empty;
        if (markers.Length == 0)
        {
            return new TabText($"{text}{padding}", null);
        }

        var dirtyOffset = label.IsDirty ? TextWidth.Of(text) + 1 : (int?)null;

        return new TabText($"{text} {markers}{padding}", dirtyOffset);
    }
}
