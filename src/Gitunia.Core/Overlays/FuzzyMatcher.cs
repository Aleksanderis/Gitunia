namespace Gitunia.Core.Overlays;

/// <summary>Subsequence matching for the palette: every query character must appear in order, case-insensitive.</summary>
public static class FuzzyMatcher
{
    private const int ConsecutiveBonus = 5;
    private const int WordStartBonus = 3;
    private const int ContiguousBonus = 20;
    private const int LengthDivisor = 8;

    /// <summary>A score (higher is better) or null when <paramref name="query"/> does not match <paramref name="text"/>.</summary>
    public static int? Score(string query, string text)
    {
        if (query.Length == 0)
        {
            return 0;
        }

        var score = 0;
        var queryIndex = 0;
        var previousMatched = false;
        for (var index = 0; index < text.Length && queryIndex < query.Length; index++)
        {
            if (char.ToLowerInvariant(text[index]) != char.ToLowerInvariant(query[queryIndex]))
            {
                previousMatched = false;
                continue;
            }

            score += 1;
            score += previousMatched ? ConsecutiveBonus : 0;
            score += index == 0 || !char.IsLetterOrDigit(text[index - 1]) ? WordStartBonus : 0;
            previousMatched = true;
            queryIndex++;
        }

        if (queryIndex < query.Length)
        {
            return null;
        }

        var contiguous = text.Contains(query, StringComparison.OrdinalIgnoreCase);
        return score + (contiguous ? ContiguousBonus : 0) - (text.Length / LengthDivisor);
    }
}
