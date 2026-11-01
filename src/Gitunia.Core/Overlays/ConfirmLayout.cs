using Gitunia.Core.Text;

namespace Gitunia.Core.Overlays;

/// <summary>
/// The size of a confirm dialog, shared by the renderer and the reducer: a dialog that does not fit the screen is not
/// shown, and then the action must not start, because an answer nobody can read is no confirmation.
/// </summary>
public static class ConfirmLayout
{
    /// <summary>The rows the two answers take (<c>y</c> and <c>n</c>), each clickable on its own row.</summary>
    public const int AnswerRows = 2;

    private const int MaximumWidth = 96;
    private const int MinimumPopupWidth = 64;
    private const int ScreenMargin = 2;

    // The title, a blank row before and after the consequence and the bottom rule, besides the text and the answers.
    private const int ChromeRows = 4;

    /// <summary>The outer width of the dialog on a screen, or less than the minimum overlay width when none fits.</summary>
    public static int WidthFor(ConfirmPrompt prompt, int screenWidth)
    {
        var widest = prompt.Lines.Append(prompt.Consequence).Select(DisplayWidth.Of).DefaultIfEmpty(0).Max();
        return OverlayMetrics.FitWidth(widest, MinimumPopupWidth, MaximumWidth, screenWidth, ScreenMargin);
    }

    /// <summary>The columns a text row has for wrapped text at a dialog width.</summary>
    public static int TextWidth(int width) =>
        Math.Max(1, width - OverlayMetrics.BorderWidth - OverlayMetrics.TextPadding);

    /// <summary>Whether the dialog fits a screen of this size.</summary>
    public static bool Fits(ConfirmPrompt prompt, int screenWidth, int screenHeight)
    {
        var width = WidthFor(prompt, screenWidth);
        if (width < OverlayMetrics.MinimumWidth)
        {
            return false;
        }

        var textWidth = TextWidth(width);
        var rows = ChromeRows + AnswerRows + WordWrap.Lines(prompt.Consequence, textWidth).Count;
        foreach (var paragraph in prompt.Lines)
        {
            rows += WordWrap.Lines(paragraph, textWidth).Count;
        }

        return rows <= screenHeight - ScreenMargin;
    }
}
