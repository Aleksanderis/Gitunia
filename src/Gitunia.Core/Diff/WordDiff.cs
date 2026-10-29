namespace Gitunia.Core.Diff;

/// <summary>
/// Word-level difference of two lines, for highlighting what changed inside a changed line. Pure and bounded: lines
/// longer than <see cref="MaximumLineLength"/> are not compared, and a changed middle part too big for the table
/// is marked as a whole, so a minified file costs no more than a short one.
/// </summary>
public static class WordDiff
{
    /// <summary>Longest line (in characters) that is compared; longer ones get no word highlight.</summary>
    public const int MaximumLineLength = 2000;

    /// <summary>Largest table (tokens of the old middle times tokens of the new one) the comparison fills.</summary>
    public const int MaximumTableCells = 40_000;

    private const char WordConnector = '_';

    /// <summary>
    /// The stretches that differ between <paramref name="oldText"/> and <paramref name="newText"/>. Words, runs of
    /// whitespace and single punctuation characters are compared as units. When the lines share no word or symbol
    /// at all (they are rewritten, not edited) nothing is highlighted: the whole line is already coloured.
    /// </summary>
    public static WordDiffResult Compare(string oldText, string newText)
    {
        var isTooLong = oldText.Length > MaximumLineLength || newText.Length > MaximumLineLength;
        if (isTooLong || string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return WordDiffResult.None;
        }

        var oldTokens = Tokenize(oldText);
        var newTokens = Tokenize(newText);
        var prefix = CommonPrefix(oldText, oldTokens, newText, newTokens);
        var suffix = CommonSuffix(oldText, oldTokens, newText, newTokens, prefix);
        var oldMiddle = oldTokens.Count - prefix - suffix;
        var newMiddle = newTokens.Count - prefix - suffix;
        var oldKept = new bool[oldTokens.Count];
        var newKept = new bool[newTokens.Count];
        MarkKept(oldKept, 0, prefix);
        MarkKept(newKept, 0, prefix);
        MarkKept(oldKept, oldTokens.Count - suffix, suffix);
        MarkKept(newKept, newTokens.Count - suffix, suffix);
        if (oldMiddle > 0 && newMiddle > 0 && (long)oldMiddle * newMiddle <= MaximumTableCells)
        {
            MarkCommon(oldText, oldTokens, newText, newTokens, prefix, oldMiddle, newMiddle, oldKept, newKept);
        }

        if (!SharesSubstance(oldText, oldTokens, oldKept))
        {
            return WordDiffResult.None;
        }

        return new WordDiffResult(Spans(oldText, oldTokens, oldKept), Spans(newText, newTokens, newKept));
    }

    private static List<ChangedSpan> Tokenize(string text)
    {
        var tokens = new List<ChangedSpan>();
        var index = 0;
        while (index < text.Length)
        {
            var kind = KindOf(text[index]);
            var end = index + 1;
            while (kind != TokenKind.Symbol && end < text.Length && KindOf(text[end]) == kind)
            {
                end++;
            }

            tokens.Add(new ChangedSpan(index, end - index));
            index = end;
        }

        return tokens;
    }

    private static TokenKind KindOf(char character)
    {
        if (char.IsWhiteSpace(character))
        {
            return TokenKind.Space;
        }

        var isWord = char.IsLetterOrDigit(character) || character == WordConnector || char.IsSurrogate(character);

        return isWord ? TokenKind.Word : TokenKind.Symbol;
    }

    private static bool Same(string first, ChangedSpan firstToken, string second, ChangedSpan secondToken) =>
        firstToken.Length == secondToken.Length
        && string.CompareOrdinal(first, firstToken.Start, second, secondToken.Start, firstToken.Length) == 0;

    private static int CommonPrefix(
        string oldText,
        List<ChangedSpan> oldTokens,
        string newText,
        List<ChangedSpan> newTokens)
    {
        var count = 0;
        var limit = Math.Min(oldTokens.Count, newTokens.Count);
        while (count < limit && Same(oldText, oldTokens[count], newText, newTokens[count]))
        {
            count++;
        }

        return count;
    }

    private static int CommonSuffix(
        string oldText,
        List<ChangedSpan> oldTokens,
        string newText,
        List<ChangedSpan> newTokens,
        int prefix)
    {
        var count = 0;
        var limit = Math.Min(oldTokens.Count, newTokens.Count) - prefix;
        while (count < limit && Same(oldText, oldTokens[^(count + 1)], newText, newTokens[^(count + 1)]))
        {
            count++;
        }

        return count;
    }

    private static void MarkKept(bool[] kept, int start, int count)
    {
        for (var index = start; index < start + count; index++)
        {
            kept[index] = true;
        }
    }

    // Longest common subsequence of the middle tokens (the part between the common prefix and suffix).
    private static void MarkCommon(
        string oldText,
        List<ChangedSpan> oldTokens,
        string newText,
        List<ChangedSpan> newTokens,
        int offset,
        int oldCount,
        int newCount,
        bool[] oldKept,
        bool[] newKept)
    {
        var width = newCount + 1;
        var table = new ushort[(oldCount + 1) * width];
        for (var row = oldCount - 1; row >= 0; row--)
        {
            for (var column = newCount - 1; column >= 0; column--)
            {
                var cell = (row * width) + column;
                var below = table[((row + 1) * width) + column];
                var right = table[cell + 1];
                table[cell] = Math.Max(below, right);
                if (Same(oldText, oldTokens[offset + row], newText, newTokens[offset + column]))
                {
                    table[cell] = (ushort)(table[((row + 1) * width) + column + 1] + 1);
                }
            }
        }

        var oldIndex = 0;
        var newIndex = 0;
        while (oldIndex < oldCount && newIndex < newCount)
        {
            if (Same(oldText, oldTokens[offset + oldIndex], newText, newTokens[offset + newIndex]))
            {
                oldKept[offset + oldIndex] = true;
                newKept[offset + newIndex] = true;
                oldIndex++;
                newIndex++;
            }
            else if (table[((oldIndex + 1) * width) + newIndex] >= table[(oldIndex * width) + newIndex + 1])
            {
                oldIndex++;
            }
            else
            {
                newIndex++;
            }
        }
    }

    // True when at least one unchanged token is more than whitespace: otherwise the lines are rewritten, not edited.
    private static bool SharesSubstance(string text, List<ChangedSpan> tokens, bool[] kept)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            if (kept[index] && !char.IsWhiteSpace(text[tokens[index].Start]))
            {
                return true;
            }
        }

        return false;
    }

    // The changed tokens as stretches; a stretch absorbs unchanged whitespace between two changed tokens.
    private static List<ChangedSpan> Spans(string text, List<ChangedSpan> tokens, bool[] kept)
    {
        var spans = new List<ChangedSpan>();
        var index = 0;
        while (index < tokens.Count)
        {
            if (kept[index])
            {
                index++;
                continue;
            }

            var start = tokens[index].Start;
            var end = tokens[index].End;
            index++;
            while (index < tokens.Count && (!kept[index] || IsGapBetweenChanges(text, tokens, kept, index)))
            {
                end = tokens[index].End;
                index++;
            }

            spans.Add(new ChangedSpan(start, end - start));
        }

        return spans;
    }

    private static bool IsGapBetweenChanges(string text, List<ChangedSpan> tokens, bool[] kept, int index) =>
        char.IsWhiteSpace(text[tokens[index].Start]) && index + 1 < tokens.Count && !kept[index + 1];

    private enum TokenKind
    {
        Word,
        Space,
        Symbol,
    }
}
