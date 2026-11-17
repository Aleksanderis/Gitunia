using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

[Trait("Os", "Independent")]
public class OverlayMetricsTests
{
    [Theory]
    [InlineData(64, 62, 64)]
    [InlineData(66, 64, 66)]
    [InlineData(103, 100, 103)]
    [InlineData(80, 64, 64)]
    [InlineData(120, 100, 100)]
    public void GivenAPopupThatLeavesAColumnOrTwoBesideIt_WhenSnapping_ThenItTakesTheWholeScreen(
        int screenWidth,
        int width,
        int expected)
    {
        Assert.Equal(expected, OverlayMetrics.Snap(width, screenWidth));
    }

    [Fact]
    public void GivenAPopupTooNarrowToDraw_WhenSnapping_ThenItIsLeftAlone()
    {
        Assert.Equal(OverlayMetrics.MinimumWidth - 1, OverlayMetrics.Snap(OverlayMetrics.MinimumWidth - 1, 18));
    }
}
