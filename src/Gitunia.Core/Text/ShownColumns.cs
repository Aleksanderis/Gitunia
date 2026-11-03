using System.Text;

namespace Gitunia.Core.Text;

/// <summary>
/// Converts between the index of a character in a line of text and the screen column it is drawn at once tabs are
/// expanded to tab stops and other control characters take one column (the way the diff draws a row). Pure.
/// </summary>
public static class ShownColumns
{
    /// <summary>The screen columns that precede the character at <paramref name="index"/>.</summary>
    /// <param name="text">The line.</param>
    /// <param name="index">UTF-16 index; past the end gives the width of the whole text.</param>
    /// <param name="tabWidth">Width of a tab stop.</param>
    public static int OfIndex(string text, int index, int tabWidth)
    {
        var column = 0;
        var counter = new WidthCounter();
        var position = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (position >= index)
            {
                break;
            }

            column += Columns(rune, column, tabWidth, ref counter);
            position += rune.Utf16SequenceLength;
        }

        return column;
    }

    /// <summary>The screen columns the character at <paramref name="index"/> takes (at least 1, so a mark is visible).</summary>
    /// <param name="text">The line.</param>
    /// <param name="index">UTF-16 index of the character.</param>
    /// <param name="tabWidth">Width of a tab stop.</param>
    public static int WidthAt(string text, int index, int tabWidth)
    {
        if (index < 0 || index >= text.Length)
        {
            return 1;
        }

        var before = OfIndex(text, index, tabWidth);
        var through = OfIndex(text, index + Rune.GetRuneAt(text, index).Utf16SequenceLength, tabWidth);

        return Math.Max(1, through - before);
    }

    /// <summary>The index of the character drawn at screen column <paramref name="column"/>.</summary>
    /// <param name="text">The line.</param>
    /// <param name="column">The screen column, counted from the start of the text.</param>
    /// <param name="tabWidth">Width of a tab stop.</param>
    /// <returns>The index of the character covering the column; the last character when the column is past the end, 0 for an empty line.</returns>
    public static int IndexAt(string text, int column, int tabWidth)
    {
        var shown = 0;
        var counter = new WidthCounter();
        var position = 0;
        var last = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var width = Columns(rune, shown, tabWidth, ref counter);
            if (column < shown + width)
            {
                return position;
            }

            shown += width;
            last = position;
            position += rune.Utf16SequenceLength;
        }

        return last;
    }

    private static int Columns(Rune rune, int column, int tabWidth, ref WidthCounter counter)
    {
        if (rune.Value == '\t')
        {
            return tabWidth - (column % tabWidth);
        }

        return Rune.IsControl(rune) ? 1 : counter.Next(rune);
    }
}
