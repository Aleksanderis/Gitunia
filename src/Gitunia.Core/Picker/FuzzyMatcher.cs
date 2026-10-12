namespace Gitunia.Core.Picker;

/// <summary>Case-insensitive subsequence matching with a score: consecutive characters, word starts and early matches score higher.</summary>
public static class FuzzyMatcher
{
    private const int MatchScore = 10;
    private const int ConsecutiveBonus = 15;
    private const int WordStartBonus = 20;
    private const int LeadingGapLimit = 10;

    /// <summary>The score of <paramref name="query"/> in <paramref name="text"/>, or null when the characters do not occur in order. An empty query scores 0.</summary>
    public static int? Score(string query, string text)
    {
        if (query.Length == 0)
        {
            return 0;
        }

        if (query.Length > text.Length)
        {
            return null;
        }

        // previous[position]: the best score of the query so far with its last character matched at that text position.
        var previous = new int?[text.Length];
        for (var queryIndex = 0; queryIndex < query.Length; queryIndex++)
        {
            var current = new int?[text.Length];
            var wanted = char.ToLowerInvariant(query[queryIndex]);
            int? bestEarlier = null;
            for (var position = queryIndex; position < text.Length; position++)
            {
                // Best of previous[k] + k over k <= position - 2: a gap of position - k - 1 skipped characters costs that many points.
                if (queryIndex > 0 && position >= 2 && previous[position - 2] is { } older)
                {
                    bestEarlier = Math.Max(bestEarlier ?? int.MinValue, older + (position - 2));
                }

                if (char.ToLowerInvariant(text[position]) != wanted)
                {
                    continue;
                }

                var gained = MatchScore + Bonus(text, position);
                if (queryIndex == 0)
                {
                    current[position] = gained - Math.Min(position, LeadingGapLimit);
                    continue;
                }

                int? best = bestEarlier is { } earlier ? earlier - (position - 1) : null;
                if (previous[position - 1] is { } adjacent)
                {
                    best = Math.Max(best ?? int.MinValue, adjacent + ConsecutiveBonus);
                }

                if (best is { } found)
                {
                    current[position] = found + gained;
                }
            }

            previous = current;
        }

        int? result = null;
        foreach (var score in previous)
        {
            if (score is { } value && (result is null || value > result))
            {
                result = value;
            }
        }

        return result;
    }

    private static int Bonus(string text, int position)
    {
        if (position == 0)
        {
            return WordStartBonus;
        }

        var before = text[position - 1];
        return before is '/' or '\\' or '-' or '_' or '.' or ' ' ? WordStartBonus : 0;
    }
}
