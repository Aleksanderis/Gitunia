using Gitunia.Core.Search;
using Gitunia.Core.Search.Models;
using Xunit;

namespace Gitunia.Core.Tests.Search;

public class SearchQueryParserTests
{
    [Theory]
    [InlineData("fix bug", SearchField.Message, "fix bug")]
    [InlineData("author:Alice", SearchField.Author, "Alice")]
    [InlineData("Author: alice ", SearchField.Author, "alice")]
    [InlineData("hash:ABC12", SearchField.Hash, "ABC12")]
    [InlineData("hash:author:x", SearchField.Hash, "author:x")]
    [InlineData("authors", SearchField.Message, "authors")]
    [InlineData("Body: Closes #4 ", SearchField.Body, "Closes #4")]
    public void GivenText_WhenParsing_ThenFieldAndTextAreSplit(string input, SearchField field, string text)
    {
        Assert.Equal(new SearchQuery(field, text), SearchQueryParser.Parse(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("author:")]
    [InlineData("hash:  ")]
    [InlineData("body:")]
    public void GivenNothingToLookFor_WhenParsing_ThenThereIsNoQuery(string input)
    {
        Assert.Null(SearchQueryParser.Parse(input));
    }
}
