namespace Gitunia.Tui.Rendering.Styling;

/// <summary>Formats the text of a padded button label for the glyph tier.</summary>
public static class ButtonLabel
{
    private const char Padding = ' ';
    private const char AsciiOpen = '[';
    private const char AsciiClose = ']';

    /// <summary>The label itself, or in ASCII the same label with brackets in place of its outer padding columns (the width stays the same).</summary>
    public static string For(string label, bool isAscii) =>
        isAscii && label.Length > 2 && label[0] == Padding && label[^1] == Padding ? $"{AsciiOpen}{label[1..^1]}{AsciiClose}" : label;
}
