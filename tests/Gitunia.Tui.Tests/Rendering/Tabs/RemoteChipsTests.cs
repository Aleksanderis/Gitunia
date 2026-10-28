using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.Tabs;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Tabs;

[Trait("Os", "Independent")]
public class RemoteChipsTests
{
    private const int Wide = 120;
    private const int Keyed = 150;

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

        Assert.Contains(" cancel ", text, StringComparison.Ordinal);
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
        var start = text.IndexOf(" cancel ", StringComparison.Ordinal);

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

    [Theory]
    [InlineData(DefaultMouseMap.RemoteFetchButton, "fetch")]
    [InlineData(DefaultMouseMap.RemotePullButton, "pull")]
    [InlineData(DefaultMouseMap.RemotePushButton, "push")]
    public void GivenAKeyedRow_WhenRendering_ThenEachChipShowsItsKeymapKeyBeforeItsLabel(string view, string label)
    {
        var action = DefaultMouseMap.CreateBindings().First(binding => binding.View == view).Action;
        var sequence = DefaultKeymap.CreateBindings().First(binding => binding.Action == action).Sequence;

        var text = ChromeBuilder.Tabs(Model(false), Keyed, GraphGlyphSet.Unicode).Text;

        Assert.Contains($" {KeyFormatter.Format(sequence)} {label} ", text, StringComparison.Ordinal);
        Assert.Equal(Keyed, text.Length);
    }

    [Fact]
    public void GivenARebinding_WhenRenderingAKeyedRow_ThenTheChipShowsTheNewKey()
    {
        var bindings = DefaultKeymap.CreateBindings()
            .Select(binding => binding.Action == ActionIds.RemoteFetch
                ? binding with { Sequence = KeySequenceParser.Parse("<space>X") }
                : binding);
        var keys = new ChipKeys(DefaultKeymap.Create().WithBindings(bindings));

        var text = ChromeBuilder.Tabs(Model(false) with { Keys = keys }, Keyed, GraphGlyphSet.Unicode).Text;

        Assert.Contains(" <space>X fetch ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<space>F", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARowJustBelowTheKeyedWidth_WhenRendering_ThenTheKeyTextIsDroppedAndTheChipsStay()
    {
        var boundary = Enumerable.Range(RemoteChips.MinimumWidth, 200).First(width => RemoteChips.IsKeyed(width));
        var width = boundary - 1;

        Assert.True(width >= RemoteChips.MinimumWidth);

        var text = ChromeBuilder.Tabs(Model(false), width, GraphGlyphSet.Unicode).Text;

        Assert.DoesNotContain("<space>", text, StringComparison.Ordinal);
        Assert.Contains(" fetch ", text, StringComparison.Ordinal);
        Assert.Contains(" pull ", text, StringComparison.Ordinal);
        Assert.Contains(" push ", text, StringComparison.Ordinal);
        var atBoundary = ChromeBuilder.Tabs(Model(false), boundary, GraphGlyphSet.Unicode).Text;
        Assert.Contains("<space>F fetch", atBoundary, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAKeyedRow_WhenRendering_ThenTheKeyIsTheStatusBarKeyStyleAndTheLabelThePlainBarStyle()
    {
        var line = ChromeBuilder.Tabs(Model(false), Keyed, GraphGlyphSet.Unicode);

        Assert.Contains(line.Segments, segment => segment.Text == "<space>F" && segment.Role == SegmentRole.StatusKey);
        Assert.Contains(line.Segments, segment => segment.Text == " fetch" && segment.Role == SegmentRole.Status);
    }

    [Fact]
    public void GivenAKeyedRow_WhenRenderingTheAsciiTier_ThenTheChipsAreBracketedAndStayTheSameWidth()
    {
        var text = ChromeBuilder.Tabs(Model(false), Keyed, GraphGlyphSet.Ascii).Text;

        Assert.Contains("[<space>F fetch]", text, StringComparison.Ordinal);
        Assert.Equal(Keyed, text.Length);
    }

    [Fact]
    public void GivenAKeyedBusyRow_WhenClickingTheCancelChip_ThenItShowsItsKeyAndTheCancelViewIsHit()
    {
        var text = ChromeBuilder.Tabs(Model(true), Keyed, GraphGlyphSet.Unicode).Text;
        var start = text.IndexOf(" <space>C cancel ", StringComparison.Ordinal);

        Assert.True(start > 0);
        Assert.Equal(DefaultMouseMap.RemoteCancelButton, RemoteChips.ViewAt(Keyed, true, start + 1));
        Assert.Null(RemoteChips.ViewAt(Keyed, true, start - 1));
    }

    [Fact]
    public void GivenAKeyedRow_WhenClickingEachChip_ThenTheViewMatchesWhatIsDrawn()
    {
        var text = ChromeBuilder.Tabs(Model(false), Keyed, GraphGlyphSet.Unicode).Text;

        var fetch = text.IndexOf("<space>F", StringComparison.Ordinal);
        var pull = text.IndexOf("<space>L", StringComparison.Ordinal);
        var push = text.IndexOf(" push ", StringComparison.Ordinal) + 1;

        Assert.Equal(DefaultMouseMap.RemoteFetchButton, RemoteChips.ViewAt(Keyed, false, fetch));
        Assert.Equal(DefaultMouseMap.RemotePullButton, RemoteChips.ViewAt(Keyed, false, pull));
        Assert.Equal(DefaultMouseMap.RemotePushButton, RemoteChips.ViewAt(Keyed, false, push));
    }

    [Fact]
    public void GivenManyTabs_WhenLayingOutAKeyedRow_ThenNoTabReachesIntoTheChips()
    {
        var labels = Enumerable.Range(1, 30)
            .Select(number => new TabLabel(number, $"repo{number}", null, number == 1, false, 0, 0, false, false))
            .ToArray();

        var cells = TabBarLayout.Layout(labels, Keyed, GraphGlyphSet.Unicode);

        var chipsStart = RemoteChips.Start(Keyed)!.Value;
        Assert.All(cells, cell => Assert.True(cell.Start + cell.Width <= chipsStart));
    }

    [Fact]
    public void GivenTheRepositoryPickerIsOpen_WhenRendering_ThenNoFetchPullOrPushChipShowsButCommandsStays()
    {
        var picker = new PickerState(string.Empty, 0, [], string.Empty, null);

        var text = ChromeBuilder.Tabs(Model(false) with { Picker = picker }, Keyed, GraphGlyphSet.Unicode).Text;

        Assert.DoesNotContain("fetch", text, StringComparison.Ordinal);
        Assert.DoesNotContain("pull", text, StringComparison.Ordinal);
        Assert.DoesNotContain("push", text, StringComparison.Ordinal);
        Assert.Contains(TabBarLayout.CommandsLabel, text, StringComparison.Ordinal);
    }
}
