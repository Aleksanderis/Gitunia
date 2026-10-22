using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out a confirm dialog: the title, what will happen, what cannot be undone (in the warning colour) and the two answers, each
/// on a row of its own so a click can answer it. Centred on the screen; there is no dialog (null) when it does not fit, and then
/// the action must not start, because an answer nobody can read is no confirmation.
/// </summary>
public static class ConfirmFrameBuilder
{
    private const int MaximumWidth = 96;
    private const int MinimumPopupWidth = 64;
    private const int ScreenMargin = 2;
    private const int AnswerRows = 2;
    private const string YesKey = "y";
    private const string NoKey = "n";
    private const string NoLabel = "Cancel (Esc does the same)";
    private const string KeyGap = "  ";

    /// <summary>The dialog for a screen, or null when it does not fit.</summary>
    public static OverlayFrame? Build(ConfirmPrompt prompt, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var widest = prompt.Lines.Append(prompt.Consequence).Select(TextWidth.Of).DefaultIfEmpty(0).Max();
        var width = OverlayBox.FitWidth(widest, MinimumPopupWidth, MaximumWidth, screenWidth, ScreenMargin);
        if (width < OverlayBox.MinimumWidth)
        {
            return null;
        }

        var textWidth = Math.Max(1, OverlayBox.InnerWidth(width) - 2);
        var lines = new List<StyledLine> { OverlayBox.Title(prompt.Title, width, glyphs) };
        foreach (var paragraph in prompt.Lines)
        {
            lines.AddRange(Wrapped(paragraph, textWidth, width, glyphs, SegmentRole.Plain));
        }

        lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        lines.AddRange(Wrapped(prompt.Consequence, textWidth, width, glyphs, SegmentRole.Error));
        lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        lines.Add(Answer(YesKey, prompt.YesLabel, width, glyphs));
        lines.Add(Answer(NoKey, NoLabel, width, glyphs));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        if (lines.Count > screenHeight - ScreenMargin)
        {
            return null;
        }

        return new OverlayFrame((screenWidth - width) / 2, Math.Max(0, (screenHeight - lines.Count) / 2), lines);
    }

    /// <summary>
    /// Which answer the row <paramref name="frameLine"/> of a frame of <paramref name="frameHeight"/> rows holds,
    /// null for any other row.
    /// </summary>
    public static ConfirmAnswer? AnswerAt(int frameLine, int frameHeight)
    {
        var yesLine = frameHeight - 1 - AnswerRows;
        if (frameLine == yesLine)
        {
            return ConfirmAnswer.Yes;
        }

        return frameLine == yesLine + 1 ? ConfirmAnswer.No : null;
    }

    private static IEnumerable<StyledLine> Wrapped(
        string text,
        int textWidth,
        int width,
        GraphGlyphSet glyphs,
        SegmentRole role) =>
        HelpFrameBuilder.WrapText(text, textWidth).Select(line => OverlayBox.Text(line, width, glyphs, role));

    private static StyledLine Answer(string key, string label, int width, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(width);
        var builder = new LineBuilder(inner, glyphs.Ellipsis)
            .Append($" {key}", SegmentRole.OverlayKey)
            .Append($"{KeyGap}{label}", SegmentRole.Plain);
        return OverlayBox.Wrap(builder.Build(), glyphs);
    }
}
