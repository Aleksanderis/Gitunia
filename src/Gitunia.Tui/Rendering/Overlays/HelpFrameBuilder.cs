using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the help overlay: the keys of the focused view, one section per view in its inheritance chain, scrollable.</summary>
public static class HelpFrameBuilder
{
    private const int MaximumWidth = 64;
    private const int MaximumKeysWidth = 20;
    private const int ChromeRows = 3;
    private const int ScreenMargin = 2;

    /// <summary>Lines the content needs at a screen width (section headings, rows with long descriptions wrapped, and a blank line between sections).</summary>
    public static int ContentHeight(IReadOnlyList<HelpSection> sections, int screenWidth)
    {
        var descriptionWidth = DescriptionWidth(sections, WidthFor(screenWidth));
        return sections.Sum(section => section.Rows.Sum(row => WrapText(row.Description, descriptionWidth).Count) + 1) + Math.Max(0, sections.Count - 1);
    }

    /// <summary>Content lines that fit the screen, 0 when the overlay cannot be shown.</summary>
    public static int VisibleRows(IReadOnlyList<HelpSection> sections, int screenWidth, int screenHeight)
    {
        if (screenWidth - ScreenMargin < OverlayBox.MinimumWidth || screenHeight - ScreenMargin - ChromeRows < 1)
        {
            return 0;
        }

        return Math.Min(ContentHeight(sections, screenWidth), screenHeight - ScreenMargin - ChromeRows);
    }

    /// <summary>The highest useful scroll position.</summary>
    public static int MaximumScroll(IReadOnlyList<HelpSection> sections, int screenWidth, int screenHeight) =>
        Math.Max(0, ContentHeight(sections, screenWidth) - VisibleRows(sections, screenWidth, screenHeight));

    /// <summary>The help frame for a screen, or null when the screen is too small to show it. <paramref name="title"/> replaces the default "Keys: view" title.</summary>
    public static OverlayFrame? Build(IReadOnlyList<HelpSection> sections, string view, int scroll, int screenWidth, int screenHeight, GraphGlyphSet glyphs, string? title = null)
    {
        var rowCount = VisibleRows(sections, screenWidth, screenHeight);
        if (rowCount == 0)
        {
            return null;
        }

        var width = WidthFor(screenWidth);
        var keysWidth = KeysWidth(sections);
        var content = Flatten(sections, keysWidth, width, glyphs);
        var first = Math.Clamp(scroll, 0, Math.Max(0, content.Count - rowCount));

        var lines = new List<StyledLine> { OverlayBox.Title(title ?? $"Keys: {view}", width, glyphs) };
        lines.AddRange(content.Skip(first).Take(rowCount));
        lines.Add(OverlayBox.Text(
            content.Count > rowCount ? $"{first + 1}-{first + rowCount}/{content.Count}  list keys scroll  Esc close" : "Esc close",
            width,
            glyphs,
            SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    private static int WidthFor(int screenWidth) => Math.Min(MaximumWidth, screenWidth - ScreenMargin);

    private static int KeysWidth(IReadOnlyList<HelpSection> sections) =>
        Math.Min(MaximumKeysWidth, sections.SelectMany(section => section.Rows).Select(row => TextWidth.Of(row.Keys)).DefaultIfEmpty(0).Max());

    // Inner width minus the two-space indents before the keys and before the description.
    private static int DescriptionWidth(IReadOnlyList<HelpSection> sections, int width) =>
        Math.Max(1, OverlayBox.InnerWidth(width) - KeysWidth(sections) - 4);

    // Greedy word wrap by display width; a word longer than a line is cut between runes, keeping combining marks with their base.
    private static List<string> WrapText(string text, int columns)
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
                foreach (var rune in remaining.EnumerateRunes())
                {
                    var runeWidth = TextWidth.Of(rune);
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

    private static List<StyledLine> Flatten(IReadOnlyList<HelpSection> sections, int keysWidth, int width, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>();
        var blankKeys = new string(' ', keysWidth);
        var descriptionWidth = DescriptionWidth(sections, width);
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
        var builder = new LineBuilder(inner, glyphs.Ellipsis)
            .Append($"  {keys}", SegmentRole.OverlayKey)
            .Append($"  {description}", SegmentRole.Plain);
        return OverlayBox.Wrap(builder.Build(), glyphs);
    }
}
