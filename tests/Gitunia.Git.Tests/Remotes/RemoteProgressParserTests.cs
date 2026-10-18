using Gitunia.Git.Remotes;
using Xunit;

namespace Gitunia.Git.Tests.Remotes;

public class RemoteProgressParserTests
{
    [Theory]
    [InlineData("Receiving objects:  45% (9/20), 1.00 KiB | 1.00 MiB/s", "Receiving objects", 45)]
    [InlineData("remote: Counting objects: 100% (5/5), done.", "Counting objects", 100)]
    [InlineData("Resolving deltas:   0% (0/3)", "Resolving deltas", 0)]
    [InlineData("Writing objects: 250% (1/1)", "Writing objects", 100)]
    public void GivenAPercentLine_WhenParsing_ThenStageAndPercentAreReturned(string line, string stage, int percent)
    {
        var progress = RemoteProgressParser.Parse(line);

        Assert.NotNull(progress);
        Assert.Equal(stage, progress.Stage);
        Assert.Equal(percent, progress.Percent);
    }

    [Fact]
    public void GivenACountOnlyLine_WhenParsing_ThenThePercentIsNull()
    {
        var progress = RemoteProgressParser.Parse("remote: Enumerating objects: 5, done.");

        Assert.NotNull(progress);
        Assert.Equal("Enumerating objects", progress.Stage);
        Assert.Null(progress.Percent);
    }

    [Theory]
    [InlineData("From /tmp/origin")]
    [InlineData("To /tmp/x:y")]
    [InlineData("   abc1234..def5678  main       -> origin/main")]
    [InlineData("fatal: 128 is not good")]
    [InlineData("remote: Total 3 (delta 0), reused 0 (delta 0)")]
    [InlineData("")]
    public void GivenAnyOtherLine_WhenParsing_ThenNullIsReturned(string line)
    {
        Assert.Null(RemoteProgressParser.Parse(line));
    }
}
