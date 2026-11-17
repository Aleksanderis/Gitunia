using Gitunia.Core.Editing;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out a prompt popup: the title (with the count or position at its right end), what the answer is for, the
/// text field (after the picker's prompt glyph; its tail when the text is longer than the popup, with a cursor mark),
/// the rows under it (the highlighted one drawn as the selection bar, like the repository picker) and the answering
/// keys. Centred on the screen; null when it does not fit, and then the prompt still answers to its keys.
/// </summary>
public static class PromptPopupFrameBuilder
{
    private const int MaximumWidth = 96;
    private const int MinimumPopupWidth = 64;
    private const int ScreenMargin = 2;
    private const int FooterLines = 3;
    private const int MinimumTitleGap = 2;

    /// <summary>The popup for a screen, or null when it does not fit.</summary>
    public static OverlayFrame? Build(PromptPopup prompt, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var widest = prompt.Lines
            .Concat(prompt.Details)
            .Append(prompt.Hint)
            .Append(prompt.Count is { } count ? $"{prompt.Title}  {count}" : prompt.Title)
            .Select(TextWidth.Of)
            .DefaultIfEmpty(0)
            .Max();
        var width = OverlayBox.FitWidth(widest, MinimumPopupWidth, MaximumWidth, screenWidth, ScreenMargin);
        if (width < OverlayBox.MinimumWidth)
        {
            return null;
        }

        var textWidth = Math.Max(1, OverlayBox.InnerWidth(width) - 2);
        var lines = new List<StyledLine> { TitleLine(prompt, width, glyphs) };
        foreach (var paragraph in prompt.Lines)
        {
            lines.AddRange(HelpFrameBuilder.WrapText(paragraph, textWidth)
                .Select(line => OverlayBox.Text(line, width, glyphs)));
        }

        if (prompt.Input is { } input)
        {
            lines.Add(InputRow(input, width, glyphs));
        }

        var fixedLines = lines.Count + FooterLines;
        var rowRoom = screenHeight - ScreenMargin - fixedLines;
        var window = FitRows(prompt.Details, prompt.Highlight, rowRoom);
        lines.AddRange(window.Rows.Select((row, index) => RowLine(row, index == window.Highlight, width, glyphs)));
        lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        lines.Add(OverlayBox.Hints(
            prompt.Hints.Select(hint => new HintItem(hint.Keys, hint.Label)),
            width,
            glyphs,
            gap: PromptPopup.HintGap));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        if (lines.Count > screenHeight - ScreenMargin)
        {
            return null;
        }

        return new OverlayFrame((screenWidth - width) / 2, Math.Max(0, (screenHeight - lines.Count) / 2), lines);
    }

    // A candidate list shows at most PickerRows.VisibleRows rows and shrinks around the highlighted row on a short screen
    // instead of making the popup not fit: a prompt that is not drawn would still take Enter. Rows without a highlight
    // (the options of a choice) are kept.
    private static PickerWindow FitRows(IReadOnlyList<string> rows, int highlight, int room)
    {
        if (highlight < 0 || highlight >= rows.Count)
        {
            return new PickerWindow(rows, highlight);
        }

        var maximumRows = Math.Min(PickerRows.VisibleRows, room);
        var window = ListViewport.Of(highlight, rows.Count, maximumRows).Follow(ScrollPolicy.Centre).Window;

        return new PickerWindow([.. rows.Skip(window.Top).Take(window.RowCount)], highlight - window.Top);
    }

    private static StyledLine RowLine(string row, bool isSelected, int width, GraphGlyphSet glyphs) =>
        OverlayBox.Text(row, width, glyphs, isSelected: isSelected);

    // The title bar with the count right-aligned, as the repository picker's; the count is left out when it does not fit.
    private static StyledLine TitleLine(PromptPopup prompt, int width, GraphGlyphSet glyphs)
    {
        if (prompt.Count is not { } count)
        {
            return OverlayBox.Title(prompt.Title, width, glyphs);
        }

        var left = $" {prompt.Title}";
        var right = $"{count} ";
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append(left, SegmentRole.OverlayTitle);
        var gap = width - TextWidth.Of(left) - TextWidth.Of(right);
        if (gap >= MinimumTitleGap)
        {
            builder.Append(new string(' ', gap), SegmentRole.OverlayTitle).Append(right, SegmentRole.OverlayTitle);
        }

        return builder.Build(SegmentRole.OverlayTitle);
    }

    private static StyledLine InputRow(TextField input, int width, GraphGlyphSet glyphs)
    {
        return OverlayBox.Input(CaretField.Of($"{glyphs.Picker.Prompt} ", input), width, glyphs);
    }
}
