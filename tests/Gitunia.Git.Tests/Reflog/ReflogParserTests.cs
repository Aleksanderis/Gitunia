using Gitunia.Git.Reflog;
using Xunit;

namespace Gitunia.Git.Tests.Reflog;

public class ReflogParserTests
{
    private const string Unit = "\u001f";
    private const string Record = "\u001e";

    [Fact]
    public void GivenEntries_WhenParsing_ThenActionMessageDateAndIndexAreSplit()
    {
        var output =
            $"{Hash('a')}{Unit}HEAD@{{3 minutes ago}}{Unit}commit (amend): fix typo{Record}\n" +
            $"{Hash('b')}{Unit}HEAD@{{2 hours ago}}{Unit}checkout: moving from main to topic{Record}\n";

        var result = ReflogParser.Parse(output);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(0, result.Items[0].Index);
        Assert.Equal(Hash('a'), result.Items[0].Hash);
        Assert.Equal("commit (amend)", result.Items[0].Action);
        Assert.Equal("fix typo", result.Items[0].Message);
        Assert.Equal("3 minutes ago", result.Items[0].RelativeDate);
        Assert.Equal(1, result.Items[1].Index);
        Assert.Equal("checkout", result.Items[1].Action);
        Assert.Equal("moving from main to topic", result.Items[1].Message);
    }

    [Fact]
    public void GivenASubjectWithoutAColon_WhenParsing_ThenItIsAllAction()
    {
        var result = ReflogParser.Parse($"{Hash('a')}{Unit}main@{{now}}{Unit}branch created{Record}");

        Assert.Equal("branch created", result.Items[0].Action);
        Assert.Equal(string.Empty, result.Items[0].Message);
        Assert.Equal("now", result.Items[0].RelativeDate);
    }

    [Fact]
    public void GivenAMessageWithColons_WhenParsing_ThenOnlyTheFirstColonSplits()
    {
        var result = ReflogParser.Parse($"{Hash('a')}{Unit}HEAD@{{1 day ago}}{Unit}commit: feat: add x{Record}");

        Assert.Equal("commit", result.Items[0].Action);
        Assert.Equal("feat: add x", result.Items[0].Message);
    }

    [Fact]
    public void GivenAShortLine_WhenParsing_ThenItIsSkippedWithAWarning()
    {
        var result = ReflogParser.Parse($"{Hash('a')}{Unit}HEAD@{{now}}{Record}");

        Assert.Empty(result.Items);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParsing_ThenThereAreNoEntries()
    {
        var result = ReflogParser.Parse(string.Empty);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }

    private static string Hash(char digit) => new(digit, 40);
}
