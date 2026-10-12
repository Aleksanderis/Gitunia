namespace Gitunia.Core.Search;

/// <summary>Turns the text typed in the search box into a <see cref="SearchQuery"/>.</summary>
public static class SearchQueryParser
{
    private const string AuthorPrefix = "author:";
    private const string HashPrefix = "hash:";

    /// <summary>Parses the input; returns null while there is nothing to look for (empty, or only a prefix).</summary>
    public static SearchQuery? Parse(string input)
    {
        var field = SearchField.Message;
        var text = input;
        if (input.StartsWith(AuthorPrefix, StringComparison.OrdinalIgnoreCase))
        {
            field = SearchField.Author;
            text = input[AuthorPrefix.Length..];
        }
        else if (input.StartsWith(HashPrefix, StringComparison.OrdinalIgnoreCase))
        {
            field = SearchField.Hash;
            text = input[HashPrefix.Length..];
        }

        text = field == SearchField.Message ? text : text.Trim();
        return text.Length == 0 ? null : new SearchQuery(field, text);
    }
}
