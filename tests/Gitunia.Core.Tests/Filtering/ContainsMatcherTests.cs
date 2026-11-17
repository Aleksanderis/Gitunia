using Gitunia.Core.Filtering;
using Xunit;

namespace Gitunia.Core.Tests.Filtering;

[Trait("Os", "Independent")]
public class ContainsMatcherTests
{
    [Theory]
    [InlineData("", "anything", true)]
    [InlineData("LOG", "feature/login", true)]
    [InlineData("login", "LOGIN", true)]
    [InlineData("lgn", "login", false)]
    [InlineData("x", "", false)]
    public void GivenAQueryAndAText_WhenMatching_ThenTheWholeQueryMustBeInTheTextIgnoringCase(
        string query,
        string text,
        bool expected)
    {
        Assert.Equal(expected, ContainsMatcher.IsMatch(query, text));
        Assert.Equal(expected, ContainsMatcher.Score(query, text) is not null);
    }
}
