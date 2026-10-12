using Gitunia.Core.Workspaces;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class CommandLineParserTests
{
    [Fact]
    public void GivenNoArguments_WhenParsing_ThenNothingRequested()
    {
        var options = CommandLineParser.Parse([]);

        Assert.Null(options.RepositoryPath);
        Assert.Null(options.Error);
        Assert.False(options.PrintsFrame);
    }

    [Fact]
    public void GivenPath_WhenParsing_ThenItIsTheRepositoryPath()
    {
        Assert.Equal("some/repo", CommandLineParser.Parse(["some/repo"]).RepositoryPath);
    }

    [Fact]
    public void GivenPrintFrame_WhenParsing_ThenSizeIsRead()
    {
        var options = CommandLineParser.Parse(["repo", "--print-frame", "120x40"]);

        Assert.True(options.PrintsFrame);
        Assert.Equal(120, options.FrameWidth);
        Assert.Equal(40, options.FrameHeight);
    }

    [Theory]
    [InlineData("--print-frame")]
    [InlineData("--print-frame", "wide")]
    [InlineData("--print-frame", "0x10")]
    [InlineData("--unknown")]
    [InlineData("-h")]
    public void GivenBadArguments_WhenParsing_ThenErrorIsReported(params string[] arguments)
    {
        Assert.NotNull(CommandLineParser.Parse(arguments).Error);
    }

    [Fact]
    public void GivenTwoPaths_WhenParsing_ThenErrorIsReported()
    {
        Assert.NotNull(CommandLineParser.Parse(["one", "two"]).Error);
    }

    [Fact]
    public void GivenDiffAndZoom_WhenParsing_ThenTheyAreRead()
    {
        var options = CommandLineParser.Parse(["repo", "--print-frame", "80x24", "--diff", "--zoom", "detail"]);

        Assert.Null(options.Error);
        Assert.True(options.OpenDiff);
        Assert.Equal(PaneKind.Detail, options.Zoom);
    }

    [Fact]
    public void GivenZoomedDiff_WhenParsing_ThenTheDiffIsOpen()
    {
        var options = CommandLineParser.Parse(["repo", "--print-frame", "80x24", "--zoom", "diff"]);

        Assert.True(options.OpenDiff);
        Assert.Equal(PaneKind.Diff, options.Zoom);
    }

    [Theory]
    [InlineData("--diff")]
    [InlineData("--zoom", "graph")]
    public void GivenFrameOptionsWithoutPrintFrame_WhenParsing_ThenErrorIsReported(params string[] arguments)
    {
        Assert.NotNull(CommandLineParser.Parse(["repo", .. arguments]).Error);
    }

    [Theory]
    [InlineData("sidebar")]
    [InlineData("")]
    public void GivenUnknownZoomPane_WhenParsing_ThenErrorIsReported(string pane)
    {
        Assert.NotNull(CommandLineParser.Parse(["repo", "--print-frame", "80x24", "--zoom", pane]).Error);
    }
}
