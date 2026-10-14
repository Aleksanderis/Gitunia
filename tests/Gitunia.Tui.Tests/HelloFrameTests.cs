using Gitunia.Platform;
using Xunit;

namespace Gitunia.Tui.Tests;

public class HelloFrameTests
{
    [Fact]
    public void GivenNoUnicode_WhenRendering_ThenAsciiFrame()
    {
        var frame = HelloFrame.Render("hi", new ConsoleCapabilities(true, ColorDepth.None, false));
        Assert.Equal("+----+\n| hi |\n+----+\n", frame);
    }

    [Fact]
    public void GivenUnicode_WhenRendering_ThenBoxFrameAroundTitle()
    {
        var frame = HelloFrame.Render("hi", new ConsoleCapabilities(true, ColorDepth.None, true));
        Assert.Equal("┌────┐\n│ hi │\n└────┘\n", frame);
    }
}
