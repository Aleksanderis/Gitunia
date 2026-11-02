using Gitunia.Core.Search;
using Gitunia.Core.Search.Models;
using Xunit;

namespace Gitunia.Core.Tests.Search;

[Trait("Os", "Independent")]
public class SearchQueryParserTests
{
    [Fact]
    public void GivenText_WhenParsing_ThenFieldAndTextAreSplit()
    {
        var failures = new List<string>();
        (string input, SearchField field, string text)[] rows =
        [
            ("fix bug", SearchField.Message, "fix bug"),
            ("author:Alice", SearchField.Author, "Alice"),
            ("Author: alice ", SearchField.Author, "alice"),
            ("hash:ABC12", SearchField.Hash, "ABC12"),
            ("hash:author:x", SearchField.Hash, "author:x"),
            ("authors", SearchField.Message, "authors"),
            ("Body: Closes #4 ", SearchField.Body, "Closes #4"),
        ];

        foreach (var (input, field, text) in rows)
        {
            try
            {
                CheckFieldAndTextAreSplit(input, field, text);
            }
            catch (Exception exception)
            {
                failures.Add($"{input}, {field}, {text}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckFieldAndTextAreSplit(string input, SearchField field, string text)
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
