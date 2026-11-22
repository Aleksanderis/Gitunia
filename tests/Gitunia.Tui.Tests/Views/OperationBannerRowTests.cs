using System.Drawing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Operations;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Views;

public sealed class OperationBannerRowTests
{
    private static readonly OperationBanner Merge = new(
        "MERGING feature/x",
        "1 conflict",
        [
            new BannerChip("<space>gc", "continue", ActionIds.OperationContinue),
            new BannerChip("<space>ga", "abort", ActionIds.OperationAbort),
        ]);

    [Fact]
    public void GivenABanner_WhenTheViewsAreLaidOut_ThenTheFrameEndsAboveTheBannerRowAndTheStatusBar()
    {
        using var fixture = new MainViewFixture(Merge);

        var rows = fixture.Panes.Window.SubViews.OfType<LineView>()
            .Where(line => line.Frame.Height == 1 && line.Frame.Width == MainViewFixture.BannerScreenWidth)
            .Select(line => line.Frame.Y)
            .ToList();

        Assert.Contains(MainViewFixture.BannerScreenHeight - 1, rows);
        Assert.Contains(MainViewFixture.BannerScreenHeight - 2, rows);
        Assert.Contains(MainViewFixture.BannerScreenHeight - 3, rows);
    }

    [Fact]
    public void GivenABanner_WhenAChipIsClicked_ThenItsMouseViewIsRaised()
    {
        using var fixture = new MainViewFixture(Merge);
        var views = new List<string>();
        fixture.View.Gestured += (view, _, _) => views.Add(view);
        var banner = BannerRenderer.Build(Merge, MainViewFixture.BannerScreenWidth, GraphGlyphSet.Unicode);
        var column = banner.Line.Text.IndexOf("<space>ga", StringComparison.Ordinal);
        var row = MainViewFixture.BannerScreenHeight - 2;

        var line = fixture.Panes.Window.SubViews.OfType<LineView>().Single(view => view.Frame.Y == row);
        line.NewMouseEvent(new Mouse
        {
            Flags = MouseFlags.LeftButtonClicked,
            Position = new Point(column, 0),
            ScreenPosition = new Point(column, row),
        });

        Assert.Equal([DefaultMouseMap.BannerAbortButton], views);
    }
}
