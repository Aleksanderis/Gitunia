using Gitunia.Core.Search;
using Xunit;

namespace Gitunia.Core.Tests.Search;

public class FuzzyMatcherTests
{
    [Theory]
    [InlineData("", "anything", 0)]
    [InlineData("abc", "xaxbxc", 1)]
    [InlineData("abc", "abd", -1)]
    public void GivenPatternAndCandidate_WhenScoring_ThenSubsequencesMatch(string pattern, string candidate, int expected)
    {
        var score = FuzzyMatcher.Score(pattern, candidate);

        Assert.Equal(expected >= 0, score is not null);
    }

    [Fact]
    public void GivenConsecutiveAndBoundaryMatches_WhenScoring_ThenTheyBeatScatteredOnes()
    {
        Assert.True(FuzzyMatcher.Score("log", "feature/login") > FuzzyMatcher.Score("log", "blueprint-organizer-g"));
    }
}
