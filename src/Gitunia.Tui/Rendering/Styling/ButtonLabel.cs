namespace Gitunia.Tui.Rendering.Styling;

/// <summary>Formats the text of a padded button label for the glyph tier.</summary>
public static class ButtonLabel
{
    private const char Padding = ' ';
    private const char AsciiOpen = '[';
    private const char AsciiClose = ']';
    private const char LeftArrow = '\u2039';
    private const char RightArrow = '\u203A';
    private const char DownArrow = '\u25BE';
    private const char AsciiDownArrow = 'v';

    /// <summary>
    /// The label itself, or in ASCII the same label with the arrows as plain angle brackets (the dropdown arrow as
    /// <c>v</c>) and with square brackets in place of its outer padding columns (the width stays the same).
    /// </summary>
    public static string For(string label, bool isAscii)
    {
        if (!isAscii)
        {
            return label;
        }

        var ascii = label.Replace(LeftArrow, '<').Replace(RightArrow, '>').Replace(DownArrow, AsciiDownArrow);
        return ascii.Length > 2 && ascii[0] == Padding && ascii[^1] == Padding ? $"{AsciiOpen}{ascii[1..^1]}{AsciiClose}" : ascii;
    }
}
