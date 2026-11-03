using Gitunia.Core.Editing;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

[Trait("Os", "Independent")]
public sealed class WordBoundariesTests
{
    [Theory]
    [InlineData("foo.bar", 7, 4)]
    [InlineData("foo.bar", 4, 3)]
    [InlineData("foo.bar", 3, 0)]
    [InlineData("foo  bar", 5, 0)]
    [InlineData("  foo", 2, 0)]
    [InlineData("a_b1 c", 4, 0)]
    [InlineData("foo...  ", 8, 3)]
    [InlineData("héllo wörld", 11, 6)]
    [InlineData("привет мир", 10, 7)]
    [InlineData("ab", 0, 0)]
    [InlineData("a😀b", 4, 3)]
    [InlineData("a😀b", 3, 1)]
    [InlineData("x ❤️", 4, 2)]
    [InlineData("x 👨‍👩 y", 7, 2)]
    [InlineData("", 0, 0)]
    public void GivenAColumn_WhenGoingPrevious_ThenItLandsOnTheWordStart(string line, int column, int expected) =>
        Assert.Equal(expected, WordBoundaries.Previous(line, column));

    [Theory]
    [InlineData("foo.bar", 0, 3)]
    [InlineData("foo.bar", 3, 4)]
    [InlineData("foo.bar", 4, 7)]
    [InlineData("foo  bar", 3, 8)]
    [InlineData("  foo", 0, 5)]
    [InlineData("a_b1 c", 0, 4)]
    [InlineData("héllo wörld", 0, 5)]
    [InlineData("привет мир", 0, 6)]
    [InlineData("ab  ", 2, 4)]
    [InlineData("ab", 2, 2)]
    [InlineData("a😀b", 1, 3)]
    [InlineData("x ❤️ y", 1, 4)]
    [InlineData("e\u0301a b", 0, 3)]
    [InlineData("", 0, 0)]
    public void GivenAColumn_WhenGoingNext_ThenItLandsOnTheWordEnd(string line, int column, int expected) =>
        Assert.Equal(expected, WordBoundaries.Next(line, column));

    [Fact]
    public void GivenALoneSurrogate_WhenStepping_ThenItDoesNotThrow()
    {
        Assert.Equal(0, WordBoundaries.Previous("\ud83d", 1));
        Assert.Equal(1, WordBoundaries.Next("\ud83d", 0));
    }
}
