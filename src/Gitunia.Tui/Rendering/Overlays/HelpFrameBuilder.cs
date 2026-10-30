using Gitunia.Core.Overlays;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the help overlay: the keys of the focused view in pages (a left menu of sections or a tab row), each split
/// under group subheaders, scrollable.
/// </summary>
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
    private const int MenuFromPages = 5;
    private const int MaximumMenuWidth = 24;
    private const string MenuMarker = "> ";
    private const string MenuIndent = "  ";
    private const int MenuTrailingSpace = 1;
    private const int MenuShareDivisor = 3;
    private const int MinimumMenuDescriptionWidth = 20;

    /// <summary>
    /// Lines the content of one tab needs at a screen width (section headings, rows with long descriptions wrapped, and
    /// a blank line between sections).
    /// </summary>
    public static int ContentHeight(IReadOnlyList<HelpTab> tabs, int tab, int screenWidth)
    {
        var sections = SectionsOf(tabs, tab);
        var descriptionWidth = DescriptionWidth(tabs, ContentWidth(tabs, screenWidth));
        var rows = sections.Sum(
            section => section.Rows.Sum(
                row => WrapText(row.Text, descriptionWidth).Count + (row.GapBefore ? 1 : 0)) + 1);

        return rows + Math.Max(0, sections.Count - 1);
    }

    /// <summary>Lines the content needs when there is a single page.</summary>
    public static int ContentHeight(IReadOnlyList<HelpSection> sections, int screenWidth) =>
        ContentHeight(SinglePage(sections), 0, screenWidth);

    /// <summary>Content lines that fit the screen, 0 when the overlay cannot be shown.</summary>
    public static int VisibleRows(IReadOnlyList<HelpTab> tabs, int tab, int screenWidth, int screenHeight)
    {
        var available = screenHeight - ScreenMargin - ChromeRows - TabRowHeight(tabs, screenWidth);
        if (screenWidth - ScreenMargin < OverlayBox.MinimumWidth || available < 1)
        {
            return 0;
        }

        // With the section menu the list of sections needs its lines too, so a short page does not cut the menu.
        var needed = Math.Max(ContentHeight(tabs, tab, screenWidth), HasMenu(tabs, screenWidth) ? tabs.Count : 0);
        return Math.Min(needed, available);
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

    /// <summary>
    /// The help frame for a screen, or null when the screen is too small to show it. <paramref name="title"/> replaces
    /// the default "Keys: view" title.
    /// </summary>
    public static OverlayFrame? Build(
        IReadOnlyList<HelpSection> sections,
        string view,
        int scroll,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs,
        string? title = null) =>
        Build(SinglePage(sections), 0, view, scroll, screenWidth, screenHeight, glyphs, title);

    /// <summary>The help frame of one page, with the section menu on the left or a tab row under the title.</summary>
    public static OverlayFrame? Build(
        IReadOnlyList<HelpTab> tabs,
        int tab,
        string view,
        int scroll,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs,
        string? title = null)
    {
        var rowCount = VisibleRows(tabs, tab, screenWidth, screenHeight);
        if (rowCount == 0)
        {
            return null;
        }

        var width = WidthFor(screenWidth);
        var contentWidth = ContentWidth(tabs, screenWidth);
        var descriptionWidth = DescriptionWidth(tabs, contentWidth);
        var content = Flatten(SectionsOf(tabs, tab), KeysWidth(tabs), contentWidth, descriptionWidth, glyphs);
        var first = Math.Clamp(scroll, 0, Math.Max(0, content.Count - rowCount));

        var lines = new List<StyledLine> { OverlayBox.Title(title ?? $"Keys: {view}", width, glyphs) };
        if (HasMenu(tabs, screenWidth))
        {
            lines.AddRange(MenuRows(tabs, tab, [.. content.Skip(first).Take(rowCount)], rowCount, width, glyphs));
        }
        else
        {
            if (tabs.Count > 1)
            {
                lines.Add(TabRow(tabs, tab, width, glyphs));
            }

            lines.AddRange(content.Skip(first).Take(rowCount));
        }

        var lead = content.Count > rowCount ? $"{first + 1}-{first + rowCount}/{content.Count}" : string.Empty;
        var hints = new List<HintItem> { new("Esc", "close") };
        if (tabs.Count > 1)
        {
            hints.Add(new("Tab", HasMenu(tabs, screenWidth) ? "next section" : "next tab"));
        }

        if (content.Count > rowCount)
        {
            hints.Add(new(string.Empty, "list keys scroll"));
        }

        lines.Add(OverlayBox.Hints(hints, width, glyphs, lead));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    /// <summary>
    /// The tab under a click: <paramref name="frameLine"/> and <paramref name="frameColumn"/> are inside the frame
    /// (0 is the top border and the left border); null when the click is not on a menu entry or tab
    /// label. When the labels do not
    /// fit, the one compact label stands for the next tab.
    /// </summary>
    public static int? TabAt(
        IReadOnlyList<HelpTab> tabs,
        int tab,
        int screenWidth,
        int screenHeight,
        int frameLine,
        int frameColumn)
    {
        if (HasMenu(tabs, screenWidth))
        {
            return MenuEntryAt(tabs, tab, screenWidth, screenHeight, frameLine, frameColumn);
        }

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

    // The labels of the tab row with their columns in the frame (column 0 is the left border).
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

    // Many pages (the real help has 17): a menu of the sections on the left instead of a tab row nobody can read,
    // unless the screen is so narrow that the descriptions beside it would get too few columns.
    private static bool HasMenu(IReadOnlyList<HelpTab> tabs, int screenWidth)
    {
        if (tabs.Count < MenuFromPages)
        {
            return false;
        }

        var width = WidthFor(screenWidth);
        return DescriptionWidth(tabs, width - MenuWidth(tabs, width) - 1) >= MinimumMenuDescriptionWidth;
    }

    private static int TabRowHeight(IReadOnlyList<HelpTab> tabs, int screenWidth) =>
        tabs.Count > 1 && !HasMenu(tabs, screenWidth) ? 1 : 0;

    private static int? MenuEntryAt(
        IReadOnlyList<HelpTab> tabs,
        int tab,
        int screenWidth,
        int screenHeight,
        int frameLine,
        int frameColumn)
    {
        var rows = VisibleRows(tabs, tab, screenWidth, screenHeight);
        var entry = MenuStart(tabs.Count, tab, rows) + frameLine - 1;
        var isInMenu = frameLine >= 1
            && frameLine <= rows
            && frameColumn >= 1
            && frameColumn <= MenuWidth(tabs, WidthFor(screenWidth));
        if (!isInMenu || entry >= tabs.Count)
        {
            return null;
        }

        return entry;
    }

    private static int MenuWidth(IReadOnlyList<HelpTab> tabs, int width)
    {
        var longest = tabs.Select(tab => TextWidth.Of(tab.Title)).DefaultIfEmpty(0).Max();
        var wanted = Math.Min(MaximumMenuWidth, longest + MenuMarker.Length + MenuTrailingSpace);
        return Math.Max(1, Math.Min(wanted, (width - OverlayBox.BorderWidth) / MenuShareDivisor));
    }

    // Outer width the page content is laid out at: the whole box, or what is left of it beside the menu.
    private static int ContentWidth(IReadOnlyList<HelpTab> tabs, int screenWidth)
    {
        var width = WidthFor(screenWidth);
        return HasMenu(tabs, screenWidth) ? width - MenuWidth(tabs, width) - 1 : width;
    }

    // First menu entry shown: the active one stays in view, roughly centred.
    private static int MenuStart(int count, int active, int rows) =>
        Math.Clamp(active - rows / 2, 0, Math.Max(0, count - rows));

    private static List<StyledLine> MenuRows(
        IReadOnlyList<HelpTab> tabs,
        int active,
        List<StyledLine> content,
        int rows,
        int width,
        GraphGlyphSet glyphs)
    {
        var menuWidth = MenuWidth(tabs, width);
        var start = MenuStart(tabs.Count, active, rows);
        var rule = glyphs.For(LineDirections.Up | LineDirections.Down).ToString();
        var separator = new StyledSegment(rule, SegmentRole.Faint);
        var contentInner = OverlayBox.InnerWidth(width) - menuWidth - 1;
        var lines = new List<StyledLine>();
        for (var row = 0; row < rows; row++)
        {
            var entry = start + row;
            var menu = new LineBuilder(menuWidth, glyphs.Ellipsis);
            if (entry < tabs.Count)
            {
                var isActive = entry == active;
                var marker = isActive ? MenuMarker : MenuIndent;
                menu.Append($"{marker}{tabs[entry].Title}", isActive ? SegmentRole.SectionTitle : SegmentRole.Faint);
            }

            // A content line is [side, ...inner, side]: keep its inner part and put the menu before it.
            var inner = InnerSegments(content, row, contentInner, glyphs);
            lines.Add(OverlayBox.Wrap(new StyledLine([.. menu.Build().Segments, separator, .. inner]), glyphs));
        }

        return lines;
    }

    // The segments between the side rules of a content line, or blanks past the end of the page.
    private static List<StyledSegment> InnerSegments(List<StyledLine> content, int row, int width, GraphGlyphSet glyphs)
    {
        if (row >= content.Count)
        {
            return [.. new LineBuilder(width, glyphs.Ellipsis).Build().Segments];
        }

        var segments = content[row].Segments;
        return [.. segments.Skip(1).Take(segments.Count - OverlayBox.BorderWidth)];
    }

    private static IReadOnlyList<HelpTab> SinglePage(IReadOnlyList<HelpSection> sections) =>
        [new HelpTab(string.Empty, sections)];

    private static IReadOnlyList<HelpSection> SectionsOf(IReadOnlyList<HelpTab> tabs, int tab) =>
        tabs.Count == 0 ? [] : tabs[Math.Clamp(tab, 0, tabs.Count - 1)].Sections;

    private static int WidthFor(int screenWidth) => Math.Min(MaximumWidth, screenWidth - ScreenMargin);

    // The same keys column on every tab, so switching tabs does not move the separator.
    private static int KeysWidth(IReadOnlyList<HelpTab> tabs) =>
        Math.Min(
            MaximumKeysWidth,
            tabs.SelectMany(tab => tab.Rows).Select(row => TextWidth.Of(row.Keys)).DefaultIfEmpty(0).Max());

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

    private static List<StyledLine> Flatten(
        IReadOnlyList<HelpSection> sections,
        int keysWidth,
        int width,
        int descriptionWidth,
        GraphGlyphSet glyphs)
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
                if (row.GapBefore)
                {
                    lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
                }

                var keys = TextWidth.Fit(row.Keys, keysWidth, glyphs.Ellipsis);
                var padded = keys + new string(' ', Math.Max(0, keysWidth - TextWidth.Of(keys)));
                var wrapped = WrapText(row.Text, descriptionWidth);
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
