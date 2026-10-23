using Gitunia.Git.Conflicts;
using Xunit;

namespace Gitunia.Git.Tests.Conflicts;

public class ConflictParserTests
{
    [Fact]
    public void GivenMergeStyleMarkers_WhenParsing_ThenCommonTextAndOneHunkComeBack()
    {
        const string text = "top\n<<<<<<< HEAD\nmine\n=======\ntheirs\n>>>>>>> other\nbottom\n";

        var result = ConflictParser.Parse(text);

        Assert.Null(result.Problem);
        var parts = result.Parts!;
        Assert.Equal(3, parts.Count);
        Assert.Equal("top\n", parts[0].Common);
        var hunk = parts[1].Hunk!;
        Assert.Equal("mine\n", hunk.Ours);
        Assert.Equal("theirs\n", hunk.Theirs);
        Assert.Null(hunk.Base);
        Assert.Equal("HEAD", hunk.OursLabel);
        Assert.Equal("other", hunk.TheirsLabel);
        Assert.Equal("<<<<<<< HEAD\nmine\n=======\ntheirs\n>>>>>>> other\n", hunk.Raw);
        Assert.Equal("bottom\n", parts[2].Common);
    }

    [Fact]
    public void GivenABaseSection_WhenParsing_ThenTheBaseIsKept()
    {
        const string text = "<<<<<<< HEAD\nmine\n||||||| parent\norig\n=======\ntheirs\n>>>>>>> other\n";

        var hunk = ConflictParser.Parse(text).Parts!.Single(part => part.Hunk is not null).Hunk!;

        Assert.Equal("mine\n", hunk.Ours);
        Assert.Equal("orig\n", hunk.Base);
        Assert.Equal("theirs\n", hunk.Theirs);
    }

    [Fact]
    public void GivenCrLfLines_WhenParsing_ThenTheTerminatorsSurviveAndMarkersStillMatch()
    {
        const string text = "a\r\n<<<<<<< HEAD\r\nmine\r\n=======\r\ntheirs\r\n>>>>>>> other\r\n";

        var parts = ConflictParser.Parse(text).Parts!;

        Assert.Equal("mine\r\n", parts[1].Hunk!.Ours);
        Assert.Equal("theirs\r\n", parts[1].Hunk!.Theirs);
        Assert.Equal(text, string.Concat(parts.Select(part => part.Common ?? part.Hunk!.Raw)));
    }

    [Fact]
    public void GivenSeveralHunks_WhenParsing_ThenEachBecomesAPartInOrder()
    {
        const string text = "<<<<<<< a\n1\n=======\n2\n>>>>>>> b\nmid\n<<<<<<< a\n3\n=======\n4\n>>>>>>> b";

        var parts = ConflictParser.Parse(text).Parts!;

        Assert.Equal(3, parts.Count);
        Assert.Equal("1\n", parts[0].Hunk!.Ours);
        Assert.Equal("mid\n", parts[1].Common);
        Assert.Equal("4\n", parts[2].Hunk!.Theirs);
    }

    [Fact]
    public void GivenALongerMarkerSize_WhenParsing_ThenOnlyThatSizeClosesTheHunk()
    {
        const string text = "<<<<<<<<<< HEAD\nmine\n==========\nkeep ======= this\ntheirs\n>>>>>>>>>> other\n";

        var hunk = ConflictParser.Parse(text).Parts!.Single().Hunk!;

        Assert.Equal("mine\n", hunk.Ours);
        Assert.Equal("keep ======= this\ntheirs\n", hunk.Theirs);
    }

    [Fact]
    public void GivenALineOfEqualsOutsideAHunk_WhenParsing_ThenItStaysText()
    {
        const string text = "Title\n=======\nbody\n";

        var result = ConflictParser.Parse(text);

        Assert.Equal(text, Assert.Single(result.Parts!).Common);
    }

    [Fact]
    public void GivenAnEmptySide_WhenParsing_ThenThatSideIsEmpty()
    {
        const string text = "<<<<<<< HEAD\n=======\ntheirs\n>>>>>>> other\n";

        var hunk = ConflictParser.Parse(text).Parts!.Single().Hunk!;

        Assert.Equal(string.Empty, hunk.Ours);
        Assert.Equal("theirs\n", hunk.Theirs);
    }

    [Fact]
    public void GivenAMarkerWithNoLabel_WhenParsing_ThenTheLabelsAreEmpty()
    {
        const string text = "<<<<<<<\nmine\n=======\ntheirs\n>>>>>>>\n";

        var hunk = ConflictParser.Parse(text).Parts!.Single().Hunk!;

        Assert.Equal(string.Empty, hunk.OursLabel);
        Assert.Equal(string.Empty, hunk.TheirsLabel);
    }

    [Theory]
    [InlineData("<<<<<<< a\nmine\n=======\ntheirs\n", "never closed")]
    [InlineData("<<<<<<< a\nmine\n<<<<<<< b\n=======\nx\n>>>>>>> b\n", "second conflict opens")]
    [InlineData("<<<<<<< a\nmine\n>>>>>>> b\n", "before the separator")]
    [InlineData("<<<<<<< a\nmine\n", "never closed")]
    public void GivenUnbalancedMarkers_WhenParsing_ThenAProblemIsReportedAndNoPartsReturned(
        string text,
        string expected)
    {
        var result = ConflictParser.Parse(text);

        Assert.Null(result.Parts);
        Assert.Contains(expected, result.Problem, StringComparison.Ordinal);
        Assert.Contains("opened on line 1", result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnEmptyFile_WhenParsing_ThenThereAreNoParts()
    {
        var result = ConflictParser.Parse(string.Empty);

        Assert.Empty(result.Parts!);
    }
}
