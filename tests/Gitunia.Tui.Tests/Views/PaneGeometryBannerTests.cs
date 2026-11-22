using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.Views;

public class PaneGeometryBannerTests
{
    [Theory]
    [InlineData(30, true, 29)]
    [InlineData(30, false, 30)]
    [InlineData(ScreenLayout.MinimumHeightWithBanner - 1, true, ScreenLayout.MinimumHeightWithBanner - 1)]
    public void GivenAnOperation_WhenAskingForTheFrameHeight_ThenTheBannerRowIsTakenOffOnlyOnATallEnoughScreen(
        int height,
        bool hasBanner,
        int expected)
    {
        var geometry = Geometry(height, hasBanner);

        Assert.Equal(expected, geometry.FrameHeight);
    }

    [Fact]
    public void GivenAnOperation_WhenAskingForTheRects_ThenTheGraphAndTheWorkingDividerAreOneRowShorter()
    {
        var plain = Geometry(31, false);
        var banner = Geometry(31, true);

        Assert.Equal(plain.Rects().Graph.Height - 1, banner.Rects().Graph.Height);
        Assert.Equal(plain.WorkingDividerRect().Height - 1, banner.WorkingDividerRect().Height);
        Assert.True(banner.WorkingRects().Message.Y + banner.WorkingRects().Message.Height <= banner.FrameHeight - 1);
    }

    private static PaneGeometry Geometry(int height, bool hasBanner) =>
        new(new FakePaneChrome(new ScreenSize(120, height)), new FakeArrangement(hasBanner), new FakeFocus());

    private sealed class FakeArrangement(bool hasBanner) : IPaneArrangement
    {
        public PaneArrangement Arrangement() => PaneArrangement.Normal;

        public bool HasBanner() => hasBanner;
    }

    private sealed class FakeFocus : IWorkingTreeFocus
    {
        public PaneKind FocusedPane() => PaneKind.WorkingUnstaged;

        public string DiffView() => "WorkingUnstagedDiff";

        public bool IsDiffZoomed() => false;

        public int? ListWidth() => null;

        public WorkingTreeButtons DiffButtons() => WorkingTreeButtons.None;
    }
}
