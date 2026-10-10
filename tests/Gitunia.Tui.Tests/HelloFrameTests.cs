using Gitunia.Platform;
using Gitunia.Tui;
using Xunit;

namespace Gitunia.Tui.Tests;

public class HelloFrameTests
{
    [Fact]
    public void Ascii_frame_when_unicode_unsupported()
    {
        var frame = HelloFrame.Render("hi", new ConsoleCapabilities(true, ColorDepth.None, false));
        Assert.Equal("+----+\n| hi |\n+----+\n", frame);
    }

    [Fact]
    public void Unicode_frame_boxes_the_title()
    {
        var frame = HelloFrame.Render("hi", new ConsoleCapabilities(true, ColorDepth.None, true));
        Assert.Equal("┌────┐\n│ hi │\n└────┘\n", frame);
    }
}
