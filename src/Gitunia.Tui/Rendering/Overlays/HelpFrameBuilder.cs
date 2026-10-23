using Gitunia.Core.Overlays;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the help overlay: the keys of the focused view in tabs (the view, its inherited lists, global), each split under group subheaders, scrollable.</summary>
public static class HelpFrameBuilder
{
    private const int MaximumWidth = 96;
    private const int MaximumKeysWidth = 20;
    private const int ChromeRows = 3;
    private const int TabRowLine = 1;
    private const int ScreenMargin = 2;
    private const string KeysIndent = "  ";
    private const string ColumnPadding = " ";
    private const int ColumnSeparatorWidth = 2 * 1 + 1;

    /// <summary>Lines the content of one tab needs at a screen width (section headings, rows with long descriptions wrapped, and a blank line between sections).</summary>
    public static int ContentHeight(IReadOnlyList<HelpTab> tabs, int tab, int screenWidth)
    {
        var sections = SectionsOf(tabs, tab);
        var descriptionWidth = DescriptionWidth(tabs, WidthFor(screenWidth));
        return sections.Sum(section => section.Rows.Sum(row => WrapText(row.Description, descriptionWidth).Count) + 1) + Math.Max(0, sections.Count - 1);
    }

    /// <summary>Lines the content needs when there is a single page.</summary>
    public static int ContentHeight(IReadOnlyList<HelpSection> sections, int screenWidth) =>
        ContentHeight(SinglePage(sections), 0, screenWidth);

    /// <summary>Content lines that fit the screen, 0 when the overlay cannot be shown.</summary>
    public static int VisibleRows(IReadOnlyList<HelpTab> tabs, int tab, int screenWidth, int screenHeight)
    {
        var available = screenHeight - ScreenMargin - ChromeRows - (tabs.Count > 1 ? 1 : 0);
        if (screenWidth - ScreenMargin < OverlayBox.MinimumWidth || available < 1)
        {
            return 0;
        }

        return Math.Min(ContentHeight(tabs, tab, screenWidth), available);
    }

    /// <summary>Content lines that fit the screen when there is a single page.</summary>
    public static int VisibleRows(IReadOnlyList<HelpSection> sections, int screenWidth, int screenHeight) =>
        VisibleRows(SinglePage(sections), 0, screenWidth, screenHeight);

    /// <summary>The highest useful scroll position of a tab.</summary>
    public static int MaximumScroll(IReadOnlyList<HelpTab> tabs, int tab, int screenWidth, int screenHeight) =>
        Math.Max(0, ContentHeight(tabs, tab, screenWidth) - VisibleRows(tabs, tab, screenWidth, screenHeight));

    /// <summary>The highest useful scroll position when there is a single page.</summary>
    public static int MaximumScroll(IReadOnlyList<HelpSection> sections, int screenWidth, int screenHeight) =>
        MaximumScroll(SinglePage(sections), 0, screenWidth, screenHeight);

    /// <summary>The help frame for a screen, or null when the screen is too small to show it. <paramref name="title"/> replaces the default "Keys: view" title.</summary>
    public static OverlayFrame? Build(IReadOnlyList<HelpSection> sections, string view, int scroll, int screenWidth, int screenHeight, GraphGlyphSet glyphs, string? title = null) =>
        Build(SinglePage(sections), 0, view, scroll, screenWidth, screenHeight, glyphs, title);

    /// <summary>The help frame of one tab, with the tab row under the title when there are several tabs.</summary>
    public static OverlayFrame? Build(IReadOnlyList<HelpTab> tabs, int tab, string view, int scroll, int screenWidth, int screenHeight, GraphGlyphSet glyphs, string? title = null)
    {
        var rowCount = VisibleRows(tabs, tab, screenWidth, screenHeight);
        if (rowCount == 0)
        {
            return null;
        }

        var width = WidthFor(screenWidth);
        var content = Flatten(SectionsOf(tabs, tab), KeysWidth(tabs), width, DescriptionWidth(tabs, width), glyphs);
        var first = Math.Clamp(scroll, 0, Math.Max(0, content.Count - rowCount));

        var lines = new List<StyledLine> { OverlayBox.Title(title ?? $"Keys: {view}", width, glyphs) };
        if (tabs.Count > 1)
        {
            lines.Add(TabRow(tabs, tab, width, glyphs));
        }

        lines.AddRange(content.Skip(first).Take(rowCount));
        var hints = new List<string>();
        if (content.Count > rowCount)
        {
            hints.Add($"{first + 1}-{first + rowCount}/{content.Count}");
            hints.Add("list keys scroll");
        }

        if (tabs.Count > 1)
        {
            hints.Add("Tab next tab");
        }

        hints.Add("Esc close");
        lines.Add(OverlayBox.Text(string.Join("  ", hints), width, glyphs, SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    /// <summary>
    /// The tab under a click: <paramref name="frameLine"/> and <paramref name="frameColumn"/> are inside the frame (0 is the top
    /// border and the left border); null when the click is not on a tab label. When the labels do not fit, the one compact label
    /// stands for the next tab.
    /// </summary>
    public static int? TabAt(IReadOnlyList<HelpTab> tabs, int tab, int screenWidth, int frameLine, int frameColumn)
    {
        if (tabs.Count < 2 || frameLine != TabRowLine)
        {
            return null;
        }

        foreach (var label in TabLabels(tabs, tab, OverlayBox.InnerWidth(WidthFor(screenWidth))))
        {
            if (frameColumn >= label.Start && frameColumn < label.Start + label.Width)
            {
                return label.Tab;
            }
        }

        return null;
    }

    private readonly record struct TabLabel(string Text, int Start, int Width, int Tab, bool IsActive);

    // The labels of the tab row with their columns in the frame (the left border is column 0, the row text starts after one space).
    private static List<TabLabel> TabLabels(IReadOnlyList<HelpTab> tabs, int active, int inner)
    {
        var labels = new List<TabLabel>();
        var column = 2;
        for (var index = 0; index < tabs.Count; index++)
        {
            var text = index == active ? $"[{tabs[index].Title}]" : $" {tabs[index].Title} ";
            var width = TextWidth.Of(text);
            labels.Add(new TabLabel(text, column, width, index, index == active));
            column += width + 1;
        }

        if (column - 1 <= inner)
        {
            return labels;
        }

        // Too narrow for every label: the active tab with its position; a click moves on to the next one.
        var compact = $"[{tabs[active].Title}] {active + 1}/{tabs.Count}";
        return [new TabLabel(compact, 2, TextWidth.Of(compact), (active + 1) % tabs.Count, true)];
    }

    private static StyledLine TabRow(IReadOnlyList<HelpTab> tabs, int active, int width, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(width);
        var builder = new LineBuilder(inner, glyphs.Ellipsis).Append(" ", SegmentRole.Plain);
        var labels = TabLabels(tabs, active, inner);
        for (var index = 0; index < labels.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(" ", SegmentRole.Plain);
            }

            builder.Append(labels[index].Text, labels[index].IsActive ? SegmentRole.SectionTitle : SegmentRole.Faint);
        }

        return OverlayBox.Wrap(builder.Build(), glyphs);
    }

    private static IReadOnlyList<HelpTab> SinglePage(IReadOnlyList<HelpSection> sections) => [new HelpTab(string.Empty, sections)];

    private static IReadOnlyList<HelpSection> SectionsOf(IReadOnlyList<HelpTab> tabs, int tab) =>
        tabs.Count == 0 ? [] : tabs[Math.Clamp(tab, 0, tabs.Count - 1)].Sections;

    private static int WidthFor(int screenWidth) => Math.Min(MaximumWidth, screenWidth - ScreenMargin);

    // The same keys column on every tab, so switching tabs does not move the separator.
    private static int KeysWidth(IReadOnlyList<HelpTab> tabs) =>
        Math.Min(MaximumKeysWidth, tabs.SelectMany(tab => tab.Sections).SelectMany(section => section.Rows).Select(row => TextWidth.Of(row.Keys)).DefaultIfEmpty(0).Max());

    // Inner width minus the indent before the keys and the separator between the columns.
    private static int DescriptionWidth(IReadOnlyList<HelpTab> tabs, int width) =>
        Math.Max(1, OverlayBox.InnerWidth(width) - KeysWidth(tabs) - KeysIndent.Length - ColumnSeparatorWidth);

    // Greedy word wrap by display width; a word longer than a line is cut between runes, keeping combining marks with their base.
    public static List<string> WrapText(string text, int columns)
    {
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;
            while (TextWidth.Of(remaining) > columns)
            {
                if (current.Length > 0)
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                var length = 0;
                var width = 0;
                var counter = new WidthCounter();
                foreach (var rune in remaining.EnumerateRunes())
                {
                    var runeWidth = counter.Next(rune);
                    if (length > 0 && width + runeWidth > columns && runeWidth > 0)
                    {
                        break;
                    }

                    length += rune.Utf16SequenceLength;
                    width += runeWidth;
                }

                lines.Add(remaining[..length]);
                remaining = remaining[length..];
            }

            if (current.Length == 0)
            {
                current = remaining;
            }
            else if (TextWidth.Of(current) + 1 + TextWidth.Of(remaining) <= columns)
            {
                current = $"{current} {remaining}";
            }
            else
            {
                lines.Add(current);
                current = remaining;
            }
        }

        if (current.Length > 0 || lines.Count == 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    private static List<StyledLine> Flatten(IReadOnlyList<HelpSection> sections, int keysWidth, int width, int descriptionWidth, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>();
        var blankKeys = new string(' ', keysWidth);
        foreach (var section in sections)
        {
            if (lines.Count > 0)
            {
                lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
            }

            lines.Add(OverlayBox.Text(section.Title, width, glyphs, SegmentRole.SectionTitle));
            foreach (var row in section.Rows)
            {
                var keys = TextWidth.Fit(row.Keys, keysWidth, glyphs.Ellipsis);
                var padded = keys + new string(' ', Math.Max(0, keysWidth - TextWidth.Of(keys)));
                var wrapped = WrapText(row.Description, descriptionWidth);
                for (var index = 0; index < wrapped.Count; index++)
                {
                    lines.Add(KeysAndDescription(index == 0 ? padded : blankKeys, wrapped[index], width, glyphs));
                }
            }
        }

        return lines;
    }

    private static StyledLine KeysAndDescription(string keys, string description, int width, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(width);
        if (keys.Length == 0)
        {
            // Plain text rows (the output of a failed command) have no keys column and no separator.
            return OverlayBox.Wrap(new LineBuilder(inner, glyphs.Ellipsis).Append($"{KeysIndent}{description}", SegmentRole.Plain).Build(), glyphs);
        }

        var builder = new LineBuilder(inner, glyphs.Ellipsis)
            .Append($"{KeysIndent}{keys}", SegmentRole.OverlayKey)
            .Append($"{ColumnPadding}{glyphs.For(LineDirections.Up | LineDirections.Down)}{ColumnPadding}", SegmentRole.Faint)
            .Append(description, SegmentRole.Plain);
        return OverlayBox.Wrap(builder.Build(), glyphs);
    }
}
