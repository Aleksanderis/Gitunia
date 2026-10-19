using Xunit;

namespace Gitunia.Platform.Tests;

public class TerminalBackgroundQueryTests
{
    [Theory]
    [InlineData("\u001b]11;rgb:ffff/ffff/ffff\u001b\\", true)]
    [InlineData("\u001b]11;rgb:0000/0000/0000\u0007", false)]
    [InlineData("\u001b]11;rgb:1e1e/1e1e/2e2e\u001b\\", false)]
    [InlineData("\u001b]11;rgb:ef/f1/f5\u0007", true)]
    [InlineData("\u001b]11;rgb:f/f/f\u0007", true)]
    [InlineData("\u001b]11;rgba:ffff/ffff/ffff/ffff\u0007", true)]
    [InlineData("rgb:fdf6/e3e3/c7c7", true)]
    [InlineData("\u001b]11;rgb:ffff/ffff/ffff\\", true)]
    public void GivenAnswer_WhenParsing_ThenLuminanceDecidesLight(string answer, bool expected) =>
        Assert.Equal(expected, TerminalBackgroundQuery.ParseIsLight(answer));

    [Theory]
    [InlineData("")]
    [InlineData("\u001b]11;?\u001b\\")]
    [InlineData("rgb:zzzz/0000/0000")]
    [InlineData("rgb:ffff/ffff")]
    [InlineData("rgb:fffff/ffff/ffff")]
    public void GivenGarbage_WhenParsing_ThenUnknown(string answer) =>
        Assert.Null(TerminalBackgroundQuery.ParseIsLight(answer));

    [Fact]
    public void GivenNotInteractive_WhenQuerying_ThenNothingIsAskedAndTheAnswerIsUnknown() =>
        Assert.Null(TerminalBackgroundQuery.Query(false, "xterm-256color", TimeSpan.FromSeconds(5)));

    [Theory]
    [InlineData(null)]
    [InlineData("dumb")]
    [InlineData("linux")]
    public void GivenTerminalWithoutOsc_WhenQuerying_ThenUnknown(string? term) =>
        Assert.Null(TerminalBackgroundQuery.Query(true, term, TimeSpan.FromSeconds(5)));

    [Fact]
    public void GivenNoColor_WhenQueryingThroughThePlatform_ThenUnknown()
    {
        var capabilities = new ConsoleCapabilities(true, ColorDepth.None, true);
        Assert.Null(new TerminalPlatform(_ => "xterm").QueryLightBackground(capabilities));
    }
}
