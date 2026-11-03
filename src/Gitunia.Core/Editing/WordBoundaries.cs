using System.Globalization;
using System.Text;

namespace Gitunia.Core.Editing;

/// <summary>
/// The word definition of the message box editor (the single-line prompts can adopt it): a word is a run of letters,
/// digits and underscores, other symbols form a run of their own, whitespace separates. So <c>foo.bar</c> is three
/// steps. Steps are by text element (a base character with its combining marks, an emoji sequence), classified by
/// the first character, so a step never lands inside one. Pure functions of a line and a UTF-16 column; the caller
/// handles the line edges.
/// </summary>
public static class WordBoundaries
{
    private enum CharClass
    {
        Space,
        Word,
        Punctuation,
    }

    /// <summary>
    /// The column where the word before <paramref name="column"/> starts: skips whitespace backwards, then one run
    /// of the same class. 0 when there is none.
    /// </summary>
    public static int Previous(string line, int column)
    {
        var starts = StringInfo.ParseCombiningCharacters(line);
        var index = starts.Length;
        while (index > 0 && starts[index - 1] >= column)
        {
            index--;
        }

        while (index > 0 && ClassAt(line, starts[index - 1]) == CharClass.Space)
        {
            index--;
        }

        if (index == 0)
        {
            return 0;
        }

        var wanted = ClassAt(line, starts[index - 1]);
        while (index > 0 && ClassAt(line, starts[index - 1]) == wanted)
        {
            index--;
        }

        return index == 0 ? 0 : starts[index];
    }

    /// <summary>
    /// The column where the word after <paramref name="column"/> ends: skips whitespace forwards, then one run of
    /// the same class. The line length when there is none.
    /// </summary>
    public static int Next(string line, int column)
    {
        var starts = StringInfo.ParseCombiningCharacters(line);
        var index = 0;
        while (index < starts.Length && starts[index] < column)
        {
            index++;
        }

        while (index < starts.Length && ClassAt(line, starts[index]) == CharClass.Space)
        {
            index++;
        }

        if (index == starts.Length)
        {
            return line.Length;
        }

        var wanted = ClassAt(line, starts[index]);
        while (index < starts.Length && ClassAt(line, starts[index]) == wanted)
        {
            index++;
        }

        return index == starts.Length ? line.Length : starts[index];
    }

    private static CharClass ClassAt(string line, int index)
    {
        if (!Rune.TryGetRuneAt(line, index, out var rune))
        {
            return CharClass.Punctuation;
        }

        if (Rune.IsWhiteSpace(rune))
        {
            return CharClass.Space;
        }

        return Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? CharClass.Word : CharClass.Punctuation;
    }
}
