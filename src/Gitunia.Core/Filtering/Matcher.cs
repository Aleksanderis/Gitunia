namespace Gitunia.Core.Filtering;

/// <summary>Scores a text for a query by the chosen <see cref="MatcherKind"/>.</summary>
public static class Matcher
{
    /// <summary>The score of <paramref name="text"/> (higher is better), or null when it does not match.</summary>
    public static int? Score(MatcherKind kind, string query, string text) => kind switch
    {
        MatcherKind.Fuzzy => FuzzyMatcher.Score(query, text),
        MatcherKind.Contains => ContainsMatcher.Score(query, text),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
