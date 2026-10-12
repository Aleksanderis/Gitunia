using Gitunia.Core.Overlays;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the help overlay: the keys of the focused view, one section per view in its inheritance chain, scrollable.</summary>
public static class HelpFrameBuilder
{
    private const int MaximumWidth = 64;
    private const int MaximumKeysWidth = 20;
    private const int ChromeRows = 3;
    private const int ScreenMargin = 2;

    /// <summary>Lines the content needs (section headings, rows and a blank line between sections).</summary>
    public static int ContentHeight(IReadOnlyList<HelpSection> sections) =>
        sections.Sum(section => section.Rows.Count + 1) + Math.Max(0, sections.Count - 1);

    /// <summary>Content lines that fit the screen, 0 when the overlay cannot be shown.</summary>
    public static int VisibleRows(IReadOnlyList<HelpSection> sections, int screenWidth, int screenHeight)
    {
        if (screenWidth - ScreenMargin < OverlayBox.MinimumWidth || screenHeight - ScreenMargin - ChromeRows < 1)
        {
            return 0;
        }

        return Math.Min(ContentHeight(sections), screenHeight - ScreenMargin - ChromeRows);
    }

    /// <summary>The highest useful scroll position.</summary>
    public static int MaximumScroll(IReadOnlyList<HelpSection> sections, int screenWidth, int screenHeight) =>
        Math.Max(0, ContentHeight(sections) - VisibleRows(sections, screenWidth, screenHeight));

    /// <summary>The help frame for a screen, or null when the screen is too small to show it. <paramref name="title"/> replaces the default "Keys: view" title.</summary>
    public static OverlayFrame? Build(IReadOnlyList<HelpSection> sections, string view, int scroll, int screenWidth, int screenHeight, GraphGlyphSet glyphs, string? title = null)
    {
        var rowCount = VisibleRows(sections, screenWidth, screenHeight);
        if (rowCount == 0)
        {
            return null;
        }

        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        var keysWidth = Math.Min(MaximumKeysWidth, sections.SelectMany(section => section.Rows).Select(row => TextWidth.Of(row.Keys)).DefaultIfEmpty(0).Max());
        var content = Flatten(sections, keysWidth, width, glyphs);
        var first = Math.Clamp(scroll, 0, Math.Max(0, content.Count - rowCount));

        var lines = new List<StyledLine> { OverlayBox.Title(title ?? "Keys: " + view, width, glyphs) };
        lines.AddRange(content.Skip(first).Take(rowCount));
        lines.Add(OverlayBox.Text(
            content.Count > rowCount ? $"{first + 1}-{first + rowCount}/{content.Count}  list keys scroll  Esc close" : "Esc close",
            width,
            glyphs,
            SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    private static List<StyledLine> Flatten(IReadOnlyList<HelpSection> sections, int keysWidth, int width, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>();
        foreach (var section in sections)
        {
            if (lines.Count > 0)
            {
                lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
            }

            lines.Add(OverlayBox.Text(section.Title, width, glyphs, SegmentRole.OverlayTitle));
            foreach (var row in section.Rows)
            {
                var keys = TextWidth.Fit(row.Keys, keysWidth, glyphs.Ellipsis);
                var padded = keys + new string(' ', Math.Max(0, keysWidth - TextWidth.Of(keys)));
                lines.Add(KeysAndDescription(padded, row.Description, width, glyphs));
            }
        }

        return lines;
    }

    private static StyledLine KeysAndDescription(string keys, string description, int width, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(width);
        var builder = new LineBuilder(inner, glyphs.Ellipsis)
            .Append("  " + keys, SegmentRole.OverlayKey)
            .Append("  " + description, SegmentRole.Plain);
        return OverlayBox.Wrap(builder.Build(), glyphs);
    }
}
