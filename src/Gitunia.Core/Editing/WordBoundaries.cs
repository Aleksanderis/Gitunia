using System.Globalization;
using System.Text;

namespace Gitunia.Core.Editing;

/// <summary>
/// The one word definition of the text editors (message box, and the single-line prompts): a word is a run of letters,
/// digits, underscores and combining marks, punctuation is a run of its own, whitespace separates. So
/// <c>foo.bar</c> is three steps. Pure functions of a line and a UTF-16 column; the caller handles line edges.
/// </summary>
public static class WordBoundaries
{
    private enum CharClass
    {
        Space,
        Word,
        Punctuation,
    }

    /// <summary>The column where the word before <paramref name="column"/> starts: skips whitespace backwards, then one run of the same class. 0 when there is none.</summary>
    public static int Previous(string line, int column)
    {
        var index = Math.Clamp(column, 0, line.Length);
        while (index > 0 && ClassBefore(line, index) == CharClass.Space)
        {
            index -= WidthBefore(line, index);
        }

        if (index == 0)
        {
            return 0;
        }

        var wanted = ClassBefore(line, index);
        while (index > 0 && ClassBefore(line, index) == wanted)
        {
            index -= WidthBefore(line, index);
        }

        return index;
    }

    /// <summary>The column where the word after <paramref name="column"/> ends: skips whitespace forwards, then one run of the same class. The line length when there is none.</summary>
    public static int Next(string line, int column)
    {
        var index = Math.Clamp(column, 0, line.Length);
        while (index < line.Length && ClassAt(line, index) == CharClass.Space)
        {
            index += WidthAt(line, index);
        }

        if (index == line.Length)
        {
            return index;
        }

        var wanted = ClassAt(line, index);
        while (index < line.Length && ClassAt(line, index) == wanted)
        {
            index += WidthAt(line, index);
        }

        return index;
    }

    private static CharClass ClassAt(string line, int index)
    {
        return Rune.TryGetRuneAt(line, index, out var rune) ? Classify(rune) : CharClass.Punctuation;
    }

    private static CharClass ClassBefore(string line, int index) => ClassAt(line, index - WidthBefore(line, index));

    private static int WidthAt(string line, int index) => Rune.TryGetRuneAt(line, index, out var rune) ? rune.Utf16SequenceLength : 1;

    private static int WidthBefore(string line, int index) =>
        index >= 2 && char.IsLowSurrogate(line[index - 1]) && char.IsHighSurrogate(line[index - 2]) ? 2 : 1;

    private static CharClass Classify(Rune rune)
    {
        if (Rune.IsWhiteSpace(rune))
        {
            return CharClass.Space;
        }

        if (Rune.IsLetterOrDigit(rune) || rune.Value == '_')
        {
            return CharClass.Word;
        }

        return Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
            ? CharClass.Word
            : CharClass.Punctuation;
    }
}
