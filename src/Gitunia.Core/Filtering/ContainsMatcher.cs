namespace Gitunia.Core.Filtering;

/// <summary>The one case-insensitive "contains" matcher: a filter that keeps what holds the typed text, in its own order.</summary>
public static class ContainsMatcher
{
    /// <summary>Score of a match (every match is equal, so the order of the list stays).</summary>
    public const int MatchScore = 0;

    /// <summary>True when <paramref name="text"/> holds <paramref name="query"/>; an empty query matches everything.</summary>
    public static bool IsMatch(string query, string text) =>
        query.Length == 0 || text.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>The score of <paramref name="text"/> for <paramref name="query"/>, or null when it does not match.</summary>
    public static int? Score(string query, string text) => IsMatch(query, text) ? MatchScore : null;
}
