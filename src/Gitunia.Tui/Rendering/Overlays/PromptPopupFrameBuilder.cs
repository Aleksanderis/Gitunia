using Gitunia.Core.Editing;
using Gitunia.Core.Hints;
using Gitunia.Core.Lists;
using Gitunia.Core.Mouse;
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

    /// <summary>The popup for a screen, or null when it does not fit.</summary>
    public static OverlayFrame? Build(PromptPopup prompt, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var widest = prompt.Lines
            .Concat(prompt.Details)
            .Append(prompt.Hint)
            .Append(prompt.EmptyText ?? string.Empty)
            .Append(prompt.Position is { } position ? $"{prompt.Title}  {position}" : prompt.Title)
            .Select(TextWidth.Of)
            .DefaultIfEmpty(0)
            .Max();
        var width = OverlayBox.FitWidth(widest, MinimumPopupWidth, MaximumWidth, screenWidth, ScreenMargin);
        if (width < OverlayBox.MinimumWidth)
        {
            return null;
        }

        var textWidth = Math.Max(1, OverlayBox.InnerWidth(width) - 2);
        var lines = new List<StyledLine> { FilterListFrame.Title(prompt.Title, null, prompt.Position, width, glyphs) };
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
        var window = FitRows(prompt.Details, prompt.Highlight, rowRoom) with { EmptyText = prompt.EmptyText };
        var targets = new List<FrameTarget>();
        var rowLines = FilterListFrame.Rows(window, width, glyphs).ToList();
        for (var index = 0; index < rowLines.Count; index++)
        {
            if (index < window.Rows.Count)
            {
                targets.Add(RowTarget(lines.Count, index, window.Highlight, width));
            }

            lines.Add(rowLines[index]);
        }

        lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        lines.Add(OverlayBox.Hints(
            [.. prompt.Hints.Select(Declared)],
            targets,
            lines.Count,
            width,
            glyphs,
            gap: PromptPopup.HintGap));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        if (lines.Count > screenHeight - ScreenMargin)
        {
            return null;
        }

        return new OverlayFrame(
            (screenWidth - width) / 2,
            Math.Max(0, (screenHeight - lines.Count) / 2),
            lines,
            targets);
    }

    private static KeyHint Declared(PromptHint hint) =>
        new(hint.Keys, hint.Label, hint.Actions.IsDefault ? [] : hint.Actions);

    // A click on a row of a candidate list moves the highlight there (the target is the distance from the highlighted
    // row, so the window of the list does not matter); on an option of a one-key prompt (no highlight) it answers (the
    // target is the option's number).
    private static FrameTarget RowTarget(int line, int index, int highlight, int width)
    {
        var target = highlight < 0 ? index : index - highlight;

        return new FrameTarget(
            line,
            1,
            width - 1,
            View: DefaultMouseMap.PromptRowButton,
            Target: target);
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

    private static StyledLine InputRow(TextField input, int width, GraphGlyphSet glyphs)
    {
        return OverlayBox.Input(CaretField.Of($"{glyphs.Picker.Prompt} ", input), width, glyphs);
    }
}
