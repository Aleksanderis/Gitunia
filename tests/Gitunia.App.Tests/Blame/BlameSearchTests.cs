using Gitunia.App.Blame;
using Gitunia.Git.Blame.Models;
using Xunit;

namespace Gitunia.App.Tests.Blame;

public class BlameSearchTests
{
    private static readonly DateTimeOffset Time = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-1, true, 1, 1)]
    [InlineData(1, true, 1, 3)]
    [InlineData(3, true, 1, 1)]
    [InlineData(3, true, 2, 3)]
    [InlineData(3, false, 1, 1)]
    [InlineData(1, false, 1, 3)]
    [InlineData(5, false, 2, 1)]
    public void GivenMatchesOnLinesOneAndThree_WhenStepping_ThenItWrapsAndSkipsByCount(
        int start,
        bool forward,
        int count,
        int expected)
    {
        var data = Data(["a", "needle", "b", "NEEDLE here", "c"]);

        Assert.Equal(expected, BlameSearch.Find(data, "needle", start, forward, count));
    }

    [Fact]
    public void GivenNoMatchOrNoLines_WhenFinding_ThenMinusOneIsReturned()
    {
        Assert.Equal(-1, BlameSearch.Find(Data(["a", "b"]), "zzz", -1, true, 1));
        Assert.Equal(-1, BlameSearch.Find(Data([]), "a", -1, true, 1));
    }

    [Fact]
    public void GivenAnAuthorOrSubject_WhenFinding_ThenTheLineOfThatCommitMatchesToo()
    {
        var data = Data(["a", "b", "c"]);
        data.Apply(new BlameChunk(2, 1, 0, new BlameCommit("1", "Ada Lovelace", Time, "Fix parser", false, "f")));

        Assert.Equal(2, BlameSearch.Find(data, "lovelace", -1, true, 1));
        Assert.Equal(2, BlameSearch.Find(data, "parser", -1, true, 1));
    }

    private static BlameData Data(string[] lines) => new(1, lines);
}
