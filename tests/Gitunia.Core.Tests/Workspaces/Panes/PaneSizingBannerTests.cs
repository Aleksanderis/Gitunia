using Gitunia.Core.Workspaces.Panes;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Panes;

public class PaneSizingBannerTests
{
    [Theory]
    [InlineData(24, true, 1)]
    [InlineData(PaneSizing.MinimumHeightWithBanner, true, 1)]
    [InlineData(PaneSizing.MinimumHeightWithBanner - 1, true, 0)]
    [InlineData(24, false, 0)]
    public void GivenAScreenAndAnOperation_WhenAskingForBannerRows_ThenOnlyATallEnoughScreenReservesTheRow(
        int height,
        bool hasBanner,
        int expected)
    {
        Assert.Equal(expected, PaneSizing.BannerRows(height, hasBanner));
    }

    [Fact]
    public void GivenABanner_WhenSizingTheArea_ThenTheGraphAreaIsOneRowShorter()
    {
        var withBanner = PaneSizing.AreaHeight(PaneSizing.FrameHeight(30, true));

        Assert.Equal(PaneSizing.AreaHeight(30) - 1, withBanner);
        Assert.Equal(PaneSizing.AreaHeight(5), PaneSizing.AreaHeight(PaneSizing.FrameHeight(5, true)));
    }
}
