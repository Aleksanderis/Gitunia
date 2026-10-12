using Gitunia.Core.Picker;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

public class FuzzyMatcherTests
{
    [Fact]
    public void GivenEmptyQuery_WhenScoring_ThenEverythingMatches()
    {
        Assert.Equal(0, FuzzyMatcher.Score(string.Empty, "anything"));
    }

    [Fact]
    public void GivenCharactersOutOfOrder_WhenScoring_ThenNoMatch()
    {
        Assert.Null(FuzzyMatcher.Score("ba", "ab"));
    }

    [Fact]
    public void GivenMissingCharacter_WhenScoring_ThenNoMatch()
    {
        Assert.Null(FuzzyMatcher.Score("gx", "gitunia"));
    }

    [Fact]
    public void GivenDifferentCase_WhenScoring_ThenItStillMatches()
    {
        Assert.NotNull(FuzzyMatcher.Score("GIT", "gitunia"));
    }

    [Fact]
    public void GivenContiguousAndScatteredMatch_WhenScoring_ThenContiguousScoresHigher()
    {
        var contiguous = FuzzyMatcher.Score("git", "gitunia");
        var scattered = FuzzyMatcher.Score("git", "gxxixxt");

        Assert.True(contiguous > scattered);
    }

    [Fact]
    public void GivenMatchAtWordStart_WhenScoring_ThenItBeatsMatchInTheMiddle()
    {
        var wordStart = FuzzyMatcher.Score("bar", "foo-bar");
        var middle = FuzzyMatcher.Score("bar", "fooxbar");

        Assert.True(wordStart > middle);
    }

    [Fact]
    public void GivenQueryLongerThanText_WhenScoring_ThenNoMatch()
    {
        Assert.Null(FuzzyMatcher.Score("abcd", "abc"));
    }
}
