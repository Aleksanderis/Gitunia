using Gitunia.Core.Graph;
using Gitunia.Core.Search.Models;

namespace Gitunia.Core.Search;

/// <summary>Decides whether a graph row matches a query. Case-insensitive, ordinal.</summary>
public static class GraphSearchMatcher
{
    public static bool IsMatch(SearchQuery query, GraphEntry entry) => query.Field switch
    {
        SearchField.Message => entry.Subject.Contains(query.Text, StringComparison.OrdinalIgnoreCase),
        SearchField.Author => entry.AuthorName.Contains(query.Text, StringComparison.OrdinalIgnoreCase),
        _ => entry.Id.ToString().StartsWith(query.Text, StringComparison.OrdinalIgnoreCase),
    };

    /// <summary>Where the query text sits in the subject, for highlighting. False when the query is not a message query or does not occur.</summary>
    public static bool TryFindInSubject(SearchQuery query, string subject, out int start, out int length)
    {
        start = query.Field == SearchField.Message ? subject.IndexOf(query.Text, StringComparison.OrdinalIgnoreCase) : -1;
        length = query.Text.Length;
        return start >= 0;
    }
}
