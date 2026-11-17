namespace Gitunia.Core.Filtering;

/// <summary>
/// The one fuzzy matcher (docs/architecture.md, "UI components"): every pattern character must appear in order, ignoring
/// case. Consecutive and word-start characters score higher, and so does a candidate that holds the pattern whole.
/// </summary>
public static class FuzzyMatcher
{
    private const int ContiguousBonus = 20;
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

        // A candidate that holds the pattern whole scores as that run when it beats the letter-by-letter match; a shorter
        // candidate is a tighter match for the same letters.
        var best = Math.Max(score, WholeScore(pattern, candidate));

        return best - (candidate.Length - pattern.Length);
    }

    private static int WholeScore(string pattern, string candidate)
    {
        var start = candidate.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return int.MinValue;
        }

        var isBoundary = start == 0 || candidate[start - 1] is '/' or '-' or '_' or '.' or ' ';
        var run = (pattern.Length * CharacterScore) + ((pattern.Length - 1) * ConsecutiveBonus);

        return run + (isBoundary ? BoundaryBonus : 0) - (start * GapPenalty) + ContiguousBonus;
    }
}
