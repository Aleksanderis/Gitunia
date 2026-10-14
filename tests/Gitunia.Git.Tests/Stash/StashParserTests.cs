using Gitunia.Git.Stash;
using Xunit;

namespace Gitunia.Git.Tests.Stash;

public class StashParserTests
{
    private const string Hash = "1111111111111111111111111111111111111111";

    private static string Record(string selector, string subject) => selector + "\0" + Hash + "\0" + subject + "\x1e\n";

    [Fact]
    public void GivenTwoStashes_WhenParsing_ThenIndexNameAndMessageAreRead()
    {
        var output = Record("stash@{0}", "On main: second") + Record("stash@{1}", "WIP on main: 1a2b3c4 first");

        var result = StashParser.Parse(output);

        Assert.Empty(result.Warnings);
        Assert.Equal([0, 1], result.Items.Select(entry => entry.Index));
        Assert.Equal("stash@{1}", result.Items[1].Name);
        Assert.Equal("WIP on main: 1a2b3c4 first", result.Items[1].Message);
        Assert.Equal(Hash, result.Items[0].CommitHash);
    }

    [Fact]
    public void GivenMessageWithNewlineAndBraces_WhenParsing_ThenItStaysOneEntry()
    {
        var result = StashParser.Parse(Record("stash@{0}", "On main: odd } message \n second line"));

        var entry = Assert.Single(result.Items);
        Assert.Equal("On main: odd } message \n second line", entry.Message);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParsing_ThenNothingAndNoWarning()
    {
        var result = StashParser.Parse(string.Empty);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("garbage\x1e")]
    [InlineData("stash@{x}\0abc\0msg\x1e")]
    [InlineData("stash@{0}\0abc\x1e")]
    public void GivenMalformedRecord_WhenParsing_ThenItIsSkippedWithAWarning(string output)
    {
        var result = StashParser.Parse(output);

        Assert.Empty(result.Items);
        Assert.Single(result.Warnings);
    }
}
