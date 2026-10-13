namespace Gitunia.Core.Search;

/// <summary>Subsequence matching with a score, for pickers: every pattern character must appear in order, ignoring case.</summary>
public static class FuzzyMatcher
{
    private const int CharacterScore = 10;
    private const int ConsecutiveBonus = 15;
    private const int BoundaryBonus = 20;
    private const int GapPenalty = 1;

    /// <summary>The score of <paramref name="candidate"/> for <paramref name="pattern"/> (higher is better), or null when it does not match.</summary>
    public static int? Score(string pattern, string candidate)
    {
        if (pattern.Length == 0)
        {
            return 0;
        }

        var score = 0;
        var position = 0;
        var previous = -2;
        foreach (var wanted in pattern)
        {
            var found = -1;
            for (var index = position; index < candidate.Length; index++)
            {
                if (char.ToLowerInvariant(candidate[index]) == char.ToLowerInvariant(wanted))
                {
                    found = index;
                    break;
                }
            }

            if (found < 0)
            {
                return null;
            }

            score += CharacterScore;
            if (found == previous + 1)
            {
                score += ConsecutiveBonus;
            }

            if (found == 0 || candidate[found - 1] is '/' or '-' or '_' or '.' or ' ')
            {
                score += BoundaryBonus;
            }

            score -= (found - position) * GapPenalty;
            previous = found;
            position = found + 1;
        }

        // A shorter candidate is a tighter match for the same letters.
        return score - (candidate.Length - pattern.Length);
    }
}
