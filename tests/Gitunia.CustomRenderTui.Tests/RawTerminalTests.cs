using Xunit;

namespace Gitunia.CustomRenderTui.Tests;

public class RawTerminalTests
{
    [Fact]
    public void GivenAnyTerminalOrNone_WhenReadingSize_ThenItIsPositive()
    {
        var (width, height) = RawTerminal.GetSize();

        Assert.True(width > 0);
        Assert.True(height > 0);
    }

    [Fact]
    public void GivenSupportedDesktopOs_WhenAskingIfSupported_ThenTrue() => Assert.True(RawTerminal.IsSupported);
}
