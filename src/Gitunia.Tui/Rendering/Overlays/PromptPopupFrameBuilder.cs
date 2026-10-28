using System.Globalization;
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
    private const int MaximumWidth = 96;
    private const int MinimumPopupWidth = 64;
    private const int ScreenMargin = 2;
    private const int FooterLines = 3;
    private const string Marker = "> ";
    private const string Cursor = "_";

    /// <summary>The popup for a screen, or null when it does not fit.</summary>
    public static OverlayFrame? Build(PromptPopup prompt, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var widest = prompt.Lines
            .Concat(prompt.Details)
            .Append(prompt.Hint)
            .Select(TextWidth.Of)
            .DefaultIfEmpty(0)
            .Max();
        var width = OverlayBox.FitWidth(widest, MinimumPopupWidth, MaximumWidth, screenWidth, ScreenMargin);
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

        var fixedLines = lines.Count + FooterLines;
        var rowRoom = screenHeight - ScreenMargin - fixedLines;
        lines.AddRange(FitRows(prompt.Details, rowRoom).Select(detail => OverlayBox.Text(detail, width, glyphs)));
        lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        lines.Add(OverlayBox.Text(prompt.Hint, width, glyphs, SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        if (lines.Count > screenHeight - ScreenMargin)
        {
            return null;
        }

        return new OverlayFrame((screenWidth - width) / 2, Math.Max(0, (screenHeight - lines.Count) / 2), lines);
    }

    // On a short screen a candidate list shrinks around the highlighted row (marked "> ") instead of making the popup
    // not fit: a prompt that is not drawn would still take Enter. A trailing "(n/m)" row is kept when two rows fit.
    private static IReadOnlyList<string> FitRows(IReadOnlyList<string> rows, int room)
    {
        if (rows.Count <= room || rows.Count == 0)
        {
            return rows;
        }

        var marked = rows.ToList().FindIndex(row => row.StartsWith(PickerRows.Mark, StringComparison.Ordinal));
        if (marked < 0)
        {
            return rows;
        }

        // Candidate rows start with two columns ("> " or spaces), so a row starting with a bracket is the position.
        var hasPosition = rows[^1].StartsWith('(');
        List<string> items = [.. rows.Take(hasPosition ? rows.Count - 1 : rows.Count)];
        var keepPosition = hasPosition && room >= 2;
        var size = Math.Max(1, room - (keepPosition ? 1 : 0));
        var first = Math.Clamp(marked - (size / 2), 0, Math.Max(0, items.Count - size));
        List<string> window = [.. items.Skip(first).Take(size)];
        if (keepPosition)
        {
            window.Add(rows[^1]);
        }

        return window;
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

        var starts = StringInfo.ParseCombiningCharacters(text);
        var used = 0;
        var keptFrom = text.Length;
        for (var index = starts.Length - 1; index >= 0; index--)
        {
            var end = index + 1 < starts.Length ? starts[index + 1] : text.Length;
            var elementWidth = TextWidth.Of(text[starts[index]..end]);
            if (used + elementWidth > columns)
            {
                break;
            }

            keptFrom = starts[index];
            used += elementWidth;
        }

        return text[keptFrom..];
    }
}
