using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out a prompt popup: the title, what the answer is for, the text field (its tail when the text is longer
/// than the popup, with a cursor mark), the rows under it and the answering keys. Centred on the screen; null when
/// it does not fit, and then the prompt still answers to its keys.
/// </summary>
public static class PromptPopupFrameBuilder
{
    private const int MaximumWidth = 64;
    private const int ScreenMargin = 2;
    private const string Marker = "> ";
    private const string Cursor = "_";

    /// <summary>The popup for a screen, or null when it does not fit.</summary>
    public static OverlayFrame? Build(PromptPopup prompt, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        if (width < OverlayBox.MinimumWidth)
        {
            return null;
        }

        var textWidth = Math.Max(1, OverlayBox.InnerWidth(width) - 2);
        var lines = new List<StyledLine> { OverlayBox.Title(prompt.Title, width, glyphs) };
        foreach (var paragraph in prompt.Lines)
        {
            lines.AddRange(HelpFrameBuilder.WrapText(paragraph, textWidth)
                .Select(line => OverlayBox.Text(line, width, glyphs)));
        }

        if (prompt.Input is { } input)
        {
            lines.Add(InputRow(input, textWidth, width, glyphs));
        }

        lines.AddRange(prompt.Details.Select(detail => OverlayBox.Text(detail, width, glyphs)));
        lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        lines.Add(OverlayBox.Text(prompt.Hint, width, glyphs, SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        if (lines.Count > screenHeight - ScreenMargin)
        {
            return null;
        }

        return new OverlayFrame((screenWidth - width) / 2, Math.Max(0, (screenHeight - lines.Count) / 2), lines);
    }

    private static StyledLine InputRow(string input, int textWidth, int width, GraphGlyphSet glyphs)
    {
        var room = Math.Max(1, textWidth - Marker.Length - Cursor.Length);
        return OverlayBox.Text($"{Marker}{Tail(input, room)}{Cursor}", width, glyphs, SegmentRole.OverlayKey);
    }

    // The end of the text that fits in the columns: the part being typed stays visible.
    private static string Tail(string text, int columns)
    {
        if (TextWidth.Of(text) <= columns)
        {
            return text;
        }

        var kept = new List<string>();
        var used = 0;
        foreach (var rune in text.EnumerateRunes().Reverse())
        {
            var runeWidth = TextWidth.Of(rune);
            if (used + runeWidth > columns)
            {
                break;
            }

            kept.Add(rune.ToString());
            used += runeWidth;
        }

        kept.Reverse();
        return string.Concat(kept);
    }
}
