using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Platform;
using Gitunia.Tui.App.CommandLine;
using Gitunia.Tui.Views.Theme;
using Xunit;

namespace Gitunia.Tui.Tests.App.CommandLine;

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
    public void GivenVersionFlag_WhenParsing_ThenVersionIsRequestedWithoutPathOrError()
    {
        var options = CommandLineParser.Parse(["--version"]);

        Assert.True(options.ShowsVersion);
        Assert.Null(options.Error);
        Assert.Null(options.RepositoryPath);
    }

    [Fact]
    public void GivenVersionFlagAmongOthers_WhenParsing_ThenVersionWins()
    {
        Assert.True(CommandLineParser.Parse(["some/repo", "--version"]).ShowsVersion);
    }

    [Fact]
    public void GivenNoVersionFlag_WhenParsing_ThenVersionIsNotRequested()
    {
        Assert.False(CommandLineParser.Parse(["some/repo"]).ShowsVersion);
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
    [InlineData("one", "two")]
    [InlineData("repo", "--print-frame", "80x24", "--tab", "tree")]
    [InlineData("repo", "--print-frame", "80x24", "--tab", "")]
    [InlineData("repo", "--print-frame", "80x24", "--tab")]
    [InlineData("repo", "--diff")]
    [InlineData("repo", "--tab", "files")]
    [InlineData("repo", "--zoom", "graph")]
    [InlineData("repo", "--print-frame", "80x24", "--zoom", "sidebar")]
    [InlineData("repo", "--print-frame", "80x24", "--zoom", "")]
    [InlineData("repo", "--print-frame", "80x24", "--colors", "rainbow")]
    [InlineData("repo", "--print-frame", "80x24", "--colors")]
    [InlineData("repo", "--print-frame", "80x24", "--light")]
    [InlineData("repo", "--colors", "16")]
    public void GivenBadArguments_WhenParsing_ThenErrorIsReported(params string[] arguments)
    {
        Assert.NotNull(CommandLineParser.Parse(arguments).Error);
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
    [InlineData("details", BottomTab.Details)]
    [InlineData("changes", BottomTab.Changes)]
    [InlineData("files", BottomTab.Files)]
    public void GivenATab_WhenParsing_ThenItIsRead(string name, BottomTab expected)
    {
        var options = CommandLineParser.Parse(["repo", "--print-frame", "80x24", "--tab", name]);

        Assert.Null(options.Error);
        Assert.Equal(expected, options.Tab);
    }

    [Fact]
    public void GivenNoTab_WhenParsing_ThenTheDefaultIsChanges()
    {
        Assert.Equal(BottomTab.Changes, CommandLineParser.Parse(["repo", "--print-frame", "80x24"]).Tab);
    }

    [Theory]
    [InlineData("truecolor", ColorDepth.TrueColor)]
    [InlineData("16", ColorDepth.Ansi16)]
    [InlineData("none", ColorDepth.None)]
    public void GivenColors_WhenParsing_ThenTheSchemeIsRead(string name, ColorDepth expected)
    {
        var options = CommandLineParser.Parse(["repo", "--print-frame", "80x24", "--colors", name, "--light"]);

        Assert.Null(options.Error);
        Assert.Equal(new PaletteSettings(expected, true), options.Colors);
    }

    [Fact]
    public void GivenNoColors_WhenParsing_ThenTheFrameIsPlainText()
    {
        Assert.Null(CommandLineParser.Parse(["repo", "--print-frame", "80x24"]).Colors);
    }
}
