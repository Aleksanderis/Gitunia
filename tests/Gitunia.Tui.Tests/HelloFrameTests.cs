using Gitunia.Platform;
using Gitunia.Tui;
using Xunit;

namespace Gitunia.Tui.Tests;

public class HelloFrameTests
{
    [Fact]
    public void Given_no_unicode_When_rendering_Then_ascii_frame()
    {
        var frame = HelloFrame.Render("hi", new ConsoleCapabilities(true, ColorDepth.None, false));
        Assert.Equal("+----+\n| hi |\n+----+\n", frame);
    }

    [Fact]
    public void Given_unicode_When_rendering_Then_box_frame_around_title()
    {
        var frame = HelloFrame.Render("hi", new ConsoleCapabilities(true, ColorDepth.None, true));
        Assert.Equal("┌────┐\n│ hi │\n└────┘\n", frame);
    }
}
