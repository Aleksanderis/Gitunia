using System.Text;

namespace Gitunia.Core.Text;

/// <summary>
/// Cursor motions inside one line of text, as vim's <c>h</c>, <c>l</c>, <c>w</c>, <c>b</c>, <c>0</c> and <c>$</c> do them in a
/// selection: they stay on the line and on a character (never past the last one). Columns are UTF-16 indexes at the
/// start of a character. Pure.
/// </summary>
public static class TextMotions
{
    /// <summary>The character before <paramref name="column"/>, or the first one.</summary>
    /// <param name="text">The line.</param>
    /// <param name="column">The current column.</param>
    /// <param name="count">How many characters to move.</param>
    public static int Left(string text, int column, int count = 1)
    {
        var result = Clamp(text, column);
        for (var step = 0; step < count && result > 0; step++)
        {
            result -= char.IsLowSurrogate(text[result - 1]) && result > 1 ? 2 : 1;
        }

        return result;
    }

    /// <summary>The character after <paramref name="column"/>, or the last one.</summary>
    /// <param name="text">The line.</param>
    /// <param name="column">The current column.</param>
    /// <param name="count">How many characters to move.</param>
    public static int Right(string text, int column, int count = 1)
    {
        var result = Clamp(text, column);
        var last = LineEnd(text);
        for (var step = 0; step < count && result < last; step++)
        {
            result += char.IsHighSurrogate(text[result]) && result + 1 < text.Length ? 2 : 1;
        }

        return Math.Min(result, last);
    }

    /// <summary>The start of the next word (a run of word characters, or of other visible characters); the last character when there is none.</summary>
    /// <param name="text">The line.</param>
    /// <param name="column">The current column.</param>
    /// <param name="count">How many words to move.</param>
    public static int WordForward(string text, int column, int count = 1)
    {
        var result = Clamp(text, column);
        for (var step = 0; step < count; step++)
        {
            result = NextWordStart(text, result);
        }

        return result;
    }

    /// <summary>The start of the word before <paramref name="column"/> (or of the current one when the column is inside it); 0 when there is none.</summary>
    /// <param name="text">The line.</param>
    /// <param name="column">The current column.</param>
    /// <param name="count">How many words to move.</param>
    public static int WordBack(string text, int column, int count = 1)
    {
        var result = Clamp(text, column);
        for (var step = 0; step < count; step++)
        {
            result = PreviousWordStart(text, result);
        }

        return result;
    }

    /// <summary>The last character of the line (0 for an empty line).</summary>
    /// <param name="text">The line.</param>
    public static int LineEnd(string text) => Clamp(text, text.Length - 1);

    /// <summary>The first and last character of the word under <paramref name="column"/>: a run of the same class (word characters, other visible characters, or white space).</summary>
    /// <param name="text">The line.</param>
    /// <param name="column">A column in the line.</param>
    public static ColumnSpan WordAt(string text, int column)
    {
        if (text.Length == 0)
        {
            return new ColumnSpan(0, 0);
        }

        var from = Clamp(text, column);
        var kind = KindAt(text, from);
        var start = from;
        while (start > 0 && KindAt(text, Left(text, start)) == kind)
        {
            start = Left(text, start);
        }

        var end = from;
        var last = LineEnd(text);
        while (end < last && KindAt(text, Right(text, end)) == kind)
        {
            end = Right(text, end);
        }

        return new ColumnSpan(start, end);
    }

    private static int NextWordStart(string text, int column)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var last = LineEnd(text);
        var kind = KindAt(text, column);
        var result = column;
        while (result < last && kind != CharacterKind.Space && KindAt(text, result) == kind)
        {
            result = Right(text, result);
        }

        while (result < last && KindAt(text, result) == CharacterKind.Space)
        {
            result = Right(text, result);
        }

        return result;
    }

    private static int PreviousWordStart(string text, int column)
    {
        var result = column;
        while (result > 0 && KindAt(text, Left(text, result)) == CharacterKind.Space)
        {
            result = Left(text, result);
        }

        if (result == 0)
        {
            return 0;
        }

        var kind = KindAt(text, Left(text, result));
        while (result > 0 && KindAt(text, Left(text, result)) == kind)
        {
            result = Left(text, result);
        }

        return result;
    }

    private static int Clamp(string text, int column)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var result = Math.Clamp(column, 0, text.Length - 1);
        return char.IsLowSurrogate(text[result]) && result > 0 ? result - 1 : result;
    }

    private static CharacterKind KindAt(string text, int column)
    {
        var rune = Rune.GetRuneAt(text, column);
        if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune))
        {
            return CharacterKind.Space;
        }

        return Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? CharacterKind.Word : CharacterKind.Punctuation;
    }

    private enum CharacterKind
    {
        Space,
        Word,
        Punctuation,
    }
}
