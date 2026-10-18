using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Tabs;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Tabs;

public class RemoteChipsTests
{
    private const int Wide = 120;

    private static ScreenModel Model(bool isBusy) => new(
        "repo",
        "main",
        1,
        1,
        "NORMAL",
        string.Empty,
        0,
        0,
        false,
        string.Empty,
        Tabs: [new TabLabel(1, "alpha", "main", true, false, 0, 0, false, false)],
        RemoteBusy: isBusy);

    [Fact]
    public void GivenAWideRow_WhenIdle_ThenFetchPullAndPushChipsSitLeftOfTheCommandsButton()
    {
        var text = ChromeBuilder.Tabs(Model(false), Wide, GraphGlyphSet.Unicode).Text;

        var fetch = text.IndexOf(" fetch ", StringComparison.Ordinal);
        var pull = text.IndexOf(" pull ", StringComparison.Ordinal);
        var push = text.IndexOf(" push ", StringComparison.Ordinal);
        var commands = text.IndexOf(TabBarLayout.CommandsLabel, StringComparison.Ordinal);
        Assert.True(fetch > 0 && fetch < pull && pull < push && push < commands);
        Assert.Equal(Wide, text.Length);
    }

    [Fact]
    public void GivenARemoteOperationRuns_WhenRendering_ThenOneCancelChipReplacesTheThree()
    {
        var text = ChromeBuilder.Tabs(Model(true), Wide, GraphGlyphSet.Unicode).Text;

        Assert.Contains(" Esc cancel ", text, StringComparison.Ordinal);
        Assert.DoesNotContain(" fetch ", text, StringComparison.Ordinal);
        Assert.DoesNotContain(" push ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARowUnderTheMinimum_WhenRendering_ThenThereAreNoChipsAndNoClickZones()
    {
        var width = RemoteChips.MinimumWidth - 1;

        var text = ChromeBuilder.Tabs(Model(false), width, GraphGlyphSet.Unicode).Text;

        Assert.DoesNotContain(" fetch ", text, StringComparison.Ordinal);
        Assert.Null(RemoteChips.Start(width));
        Assert.All(Enumerable.Range(0, width), column => Assert.Null(RemoteChips.ViewAt(width, false, column)));
    }

    [Theory]
    [InlineData(" fetch ", DefaultMouseMap.RemoteFetchButton)]
    [InlineData(" pull ", DefaultMouseMap.RemotePullButton)]
    [InlineData(" push ", DefaultMouseMap.RemotePushButton)]
    public void GivenAnIdleRow_WhenClickingAChipsColumns_ThenItsViewIsHitAndNeighboursAreNot(string label, string view)
    {
        var text = ChromeBuilder.Tabs(Model(false), Wide, GraphGlyphSet.Unicode).Text;
        var start = text.IndexOf(label, StringComparison.Ordinal);

        Assert.Equal(view, RemoteChips.ViewAt(Wide, false, start));
        Assert.Equal(view, RemoteChips.ViewAt(Wide, false, start + label.Length - 1));
        Assert.NotEqual(view, RemoteChips.ViewAt(Wide, false, start - 1));
        Assert.NotEqual(view, RemoteChips.ViewAt(Wide, false, start + label.Length));
    }

    [Fact]
    public void GivenABusyRow_WhenClickingTheCancelChip_ThenTheCancelViewIsHit()
    {
        var text = ChromeBuilder.Tabs(Model(true), Wide, GraphGlyphSet.Unicode).Text;
        var start = text.IndexOf(" Esc cancel ", StringComparison.Ordinal);

        Assert.Equal(DefaultMouseMap.RemoteCancelButton, RemoteChips.ViewAt(Wide, true, start));
        Assert.Null(RemoteChips.ViewAt(Wide, true, start - 1));
    }

    [Fact]
    public void GivenManyTabs_WhenLayingOutAWideRow_ThenNoTabReachesIntoTheChips()
    {
        var labels = Enumerable.Range(1, 30)
            .Select(number => new TabLabel(number, $"repo{number}", null, number == 1, false, 0, 0, false, false))
            .ToArray();

        var cells = TabBarLayout.Layout(labels, Wide, GraphGlyphSet.Unicode);

        var chipsStart = RemoteChips.Start(Wide)!.Value;
        Assert.All(cells, cell => Assert.True(cell.Start + cell.Width <= chipsStart));
        Assert.True(TabBarLayout.NewTabStart(cells) + TabBarLayout.NewTabLabel.Length <= chipsStart);
    }
}
