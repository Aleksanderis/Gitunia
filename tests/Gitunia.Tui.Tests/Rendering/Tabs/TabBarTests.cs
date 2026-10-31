using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.Tabs;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Tabs;

[Trait("Os", "Independent")]
public class TabBarTests
{
    private static TabLabel Label(int number, string name, bool isActive = false, string? branch = "main", bool dirty = false, int ahead = 0, int behind = 0, bool missing = false, bool loading = false) =>
        new(number, name, branch, isActive, dirty, ahead, behind, missing, loading);

    private static ScreenModel Model(params TabLabel[] labels) =>
        new("repo", "main", 1, labels.Length, "NORMAL", string.Empty, 0, 0, false, string.Empty, Tabs: labels);

    private static string Row(int width, GraphGlyphSet glyphs, params TabLabel[] labels) =>
        ChromeBuilder.Tabs(Model(labels), width, glyphs).Text.TrimEnd();

    [Fact]
    public void GivenTabsWithMarkers_WhenRendering_ThenEachTabIsAPaddedChipWithItsMarkers()
    {
        var row = Row(
            80,
            GraphGlyphSet.Unicode,
            Label(1, "alpha", isActive: true, branch: null),
            Label(2, "beta", branch: null, dirty: true, ahead: 2, behind: 1),
            Label(3, "gamma", branch: null));

        Assert.StartsWith("   1 alpha     2 beta *↑2↓1     3 gamma  ", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALabelWithABranch_WhenRendering_ThenTheBranchFollowsTheName()
    {
        var row = Row(80, GraphGlyphSet.Unicode, Label(1, "alpha", isActive: true));

        Assert.StartsWith("   1 alpha  main  ", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTabsWithoutUnicode_WhenRendering_ThenAheadAndBehindUsePlusAndMinus()
    {
        var row = Row(80, GraphGlyphSet.Ascii, Label(1, "alpha", isActive: true, branch: null, dirty: true, ahead: 3, behind: 4));

        Assert.StartsWith("   1 alpha *+3-4  ", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMissingTab_WhenRendering_ThenItIsMarkedAndShowsNoBranch()
    {
        var row = Row(80, GraphGlyphSet.Unicode, Label(1, "alpha", missing: true, branch: null), Label(2, "beta", isActive: true, branch: null));

        Assert.StartsWith("   1 ! alpha (missing)     2 beta  ", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenATabWithAnOperationInProgress_WhenRendering_ThenABangMarkerFollowsTheDirtyMarkerWithoutMovingIt()
    {
        var label = Label(1, "alpha", isActive: true, branch: null, dirty: true) with { HasOperation = true };

        var text = TabBarLayout.Layout([label], 80, GraphGlyphSet.Unicode)[0];

        Assert.StartsWith("  1 alpha *!", text.Text, StringComparison.Ordinal);
        Assert.Equal('*', text.Text[text.DirtyOffset!.Value]);
        var clean = Label(1, "alpha", isActive: true, branch: null) with { HasOperation = true };
        var cleanText = TabBarLayout.Layout([clean], 80, GraphGlyphSet.Unicode)[0].Text;
        Assert.StartsWith("  1 alpha !", cleanText, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenLoadingTab_WhenRendering_ThenTheBackgroundActivityMarkerIsShown()
    {
        var row = Row(80, GraphGlyphSet.Unicode, Label(1, "alpha", isActive: true, branch: null, loading: true));

        Assert.StartsWith("   1 alpha …  ", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheActiveAndAnInactiveTab_WhenRendering_ThenTheActiveOneIsTheChipAndTheOthersAreButtons()
    {
        var line = ChromeBuilder.Tabs(Model(Label(1, "alpha", isActive: true, branch: null), Label(2, "beta", branch: null)), 80, GraphGlyphSet.Unicode);

        Assert.Single(line.Segments, segment => segment.Role == SegmentRole.ActiveTab);
        Assert.Equal("  1 alpha  ", line.Segments.Single(segment => segment.Role == SegmentRole.ActiveTab).Text);
        Assert.Equal("  2 beta  ", line.Segments.Single(segment => segment.Role == SegmentRole.RepositoryTab).Text);
        Assert.Equal(80, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenTabsWithoutUnicode_WhenRendering_ThenInactiveTabsAreBracketedWithTheSameWidth()
    {
        var labels = new[] { Label(1, "alpha", isActive: true, branch: null), Label(2, "beta", branch: null) };
        var line = ChromeBuilder.Tabs(Model(labels), 80, GraphGlyphSet.Ascii);

        var button = line.Segments.Single(segment => segment.Role == SegmentRole.RepositoryTab);

        Assert.Equal("[ 2 beta ]", button.Text);
        Assert.Equal(TabBarLayout.Layout(labels, 80, GraphGlyphSet.Ascii)[1].Width, TextWidth.Of(button.Text));
    }

    [Fact]
    public void GivenPaletteOpenRebound_WhenRendering_ThenTheCommandsChipShowsTheNewKeyAndTheHitZoneFollows()
    {
        var bindings = DefaultKeymap.CreateBindings()
            .Select(binding => binding.Action == ActionIds.PaletteOpen && binding.View == KeyBindingTable.GlobalView && binding.Mode == KeyMode.Normal
                ? binding with { Sequence = KeySequenceParser.Parse("<space>p") }
                : binding);
        var keys = new ChipKeys(DefaultKeymap.Create().WithBindings(bindings));
        var model = Model(Label(1, "alpha", isActive: true, branch: null)) with { Keys = keys };

        var row = ChromeBuilder.Tabs(model, 80, GraphGlyphSet.Unicode);

        Assert.EndsWith(" <space>p commands  ", row.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(row.Segments, segment => segment.Role == SegmentRole.StatusKey && segment.Text == ":");
        Assert.Equal(80, row.Text.Length);
        var start = TabBarLayout.CommandsStart(80, keys)!.Value;
        Assert.True(TabBarLayout.IsOnCommands(80, start, keys));
        Assert.False(TabBarLayout.IsOnCommands(80, start - 1, keys));
        Assert.Equal(" <space>p commands ", row.Text[start..^1]);
    }

    [Fact]
    public void GivenAWideRow_WhenRendering_ThenTheCommandsButtonEndsTheRowAndNarrowRowsHaveNone()
    {
        var wide = ChromeBuilder.Tabs(Model(Label(1, "alpha", isActive: true, branch: null)), 80, GraphGlyphSet.Unicode);
        var narrow = ChromeBuilder.Tabs(Model(Label(1, "alpha", isActive: true, branch: null)), 40, GraphGlyphSet.Unicode);

        Assert.Contains(wide.Segments, segment => segment.Role == SegmentRole.StatusKey && segment.Text == ":");
        Assert.EndsWith($"{TabBarLayout.CommandsChip().Label} ", wide.Text, StringComparison.Ordinal);
        Assert.True(TabBarLayout.IsOnCommands(80, 70));
        Assert.False(TabBarLayout.IsOnCommands(80, 10));
        Assert.DoesNotContain(narrow.Segments, segment => segment.Role == SegmentRole.BarButton && segment.Text == TabBarLayout.CommandsChip().Label);
        Assert.False(TabBarLayout.IsOnCommands(40, 35));
    }

    [Fact]
    public void GivenTabs_WhenRendering_ThenThePlusButtonFollowsTheLastTabAndIsClickable()
    {
        var labels = new[] { Label(1, "alpha", isActive: true, branch: null), Label(2, "beta", branch: null) };
        var cells = TabBarLayout.Layout(labels, 80, GraphGlyphSet.Unicode);
        var line = ChromeBuilder.Tabs(Model(labels), 80, GraphGlyphSet.Unicode);

        var start = TabBarLayout.NewTabStart(cells);
        Assert.Equal(TabBarLayout.NewTabLabel, line.Text.Substring(start, TabBarLayout.NewTabLabel.Length));
        Assert.True(TabBarLayout.IsOnNewTab(cells, start + 1));
        Assert.False(TabBarLayout.IsOnNewTab(cells, start - 1));
        Assert.Null(TabBarLayout.NumberAt(cells, start));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenADirtyTab_WhenHitTesting_ThenOnlyTheStarColumnIsTheDirtyMarker(bool unicode)
    {
        var glyphs = unicode ? GraphGlyphSet.Unicode : GraphGlyphSet.Ascii;
        var labels = new[] { Label(1, "alpha", isActive: true, branch: null, dirty: true, ahead: 1), Label(2, "beta", branch: null), Label(3, "gamma", branch: null, dirty: true) };
        var cells = TabBarLayout.Layout(labels, 80, glyphs);
        var text = ChromeBuilder.Tabs(Model(labels), 80, glyphs).Text;

        var stars = Enumerable.Range(0, text.Length).Where(column => TabBarLayout.DirtyMarkerAt(cells, column) is not null).ToList();

        Assert.Equal(2, stars.Count);
        Assert.All(stars, column => Assert.Equal('*', text[column]));
        Assert.Equal(1, TabBarLayout.DirtyMarkerAt(cells, stars[0]));
        Assert.Equal(3, TabBarLayout.DirtyMarkerAt(cells, stars[1]));
        Assert.Equal(1, TabBarLayout.NumberAt(cells, stars[0]));
    }

    [Fact]
    public void GivenACutOrCleanOrMissingTab_WhenHitTesting_ThenThereIsNoDirtyMarker()
    {
        var labels = new[] { Label(1, "alpha", isActive: true, branch: null), Label(2, "beta", branch: null, dirty: true, missing: true) };
        var cells = TabBarLayout.Layout(labels, 80, GraphGlyphSet.Unicode);
        var cut = TabBarLayout.Layout([Label(1, new string('x', 60), isActive: true, branch: null, dirty: true)], 44, GraphGlyphSet.Unicode);

        Assert.All(Enumerable.Range(0, 80), column => Assert.Null(TabBarLayout.DirtyMarkerAt(cells, column)));
        Assert.All(Enumerable.Range(0, 44), column => Assert.Null(TabBarLayout.DirtyMarkerAt(cut, column)));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(60)]
    [InlineData(100)]
    public void GivenTooManyTabs_WhenLayingOut_ThenThePlusButtonStaysInsideTheRowBeforeTheCommands(int width)
    {
        var labels = Enumerable.Range(1, 14).Select(number => Label(number, $"repo{number}", isActive: number == 1, branch: null)).ToArray();
        var cells = TabBarLayout.Layout(labels, width, GraphGlyphSet.Unicode);

        var end = TabBarLayout.NewTabStart(cells) + TabBarLayout.NewTabLabel.Length;
        Assert.True(end <= (TabBarLayout.CommandsStart(width) ?? width));
    }

    [Fact]
    public void GivenMoreTabsThanFit_WhenTheLastIsActive_ThenTheRowScrollsToKeepItVisible()
    {
        var labels = Enumerable.Range(1, 8).Select(number => Label(number, $"repo{number}", isActive: number == 8, branch: null)).ToArray();

        var row = Row(60, GraphGlyphSet.Unicode, labels);

        Assert.StartsWith(" < ", row, StringComparison.Ordinal);
        Assert.Contains("8 repo8", row, StringComparison.Ordinal);
        Assert.DoesNotContain(" 1 repo1", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMoreTabsThanFit_WhenTheFirstIsActive_ThenTheRowStartsAtTheFirstTab()
    {
        var labels = Enumerable.Range(1, 8).Select(number => Label(number, $"repo{number}", isActive: number == 1, branch: null)).ToArray();

        var row = Row(60, GraphGlyphSet.Unicode, labels);

        Assert.StartsWith("   1 repo1", row, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void GivenTooManyTabs_WhenLayingOut_ThenNoTabReachesTheCommandsButtonAndEveryColumnHasOneOwner(int width)
    {
        var labels = Enumerable.Range(1, 14).Select(number => Label(number, $"repo{number}", isActive: number == 1, branch: null)).ToArray();

        var cells = TabBarLayout.Layout(labels, width, GraphGlyphSet.Unicode);

        var start = TabBarLayout.CommandsStart(width)!.Value;
        Assert.True(cells.Count < labels.Length);
        Assert.All(cells, cell => Assert.True(cell.Start + cell.Width < start));
        for (var column = 0; column < width; column++)
        {
            Assert.False(TabBarLayout.IsOnCommands(width, column) && TabBarLayout.NumberAt(cells, column) is not null);
        }
    }

    [Fact]
    public void GivenAVeryLongTabName_WhenTheActiveTabIsFirst_ThenItStillEndsBeforeTheButton()
    {
        var name = new string('x', RepositoryState40());
        var cells = TabBarLayout.Layout([Label(1, name, isActive: true, branch: null)], 60, GraphGlyphSet.Unicode);

        Assert.Equal(1, Assert.Single(cells).Number);
        Assert.True(cells[0].Start + cells[0].Width < TabBarLayout.CommandsStart(60)!.Value);
    }

    private static int RepositoryState40() => RepositoryState.MaximumNameLength;

    [Fact]
    public void GivenTabRow_WhenHitTestingColumns_ThenTheTabUnderTheColumnIsFoundAndGapsAreNot()
    {
        var labels = new[] { Label(1, "alpha", isActive: true), Label(2, "beta") };
        var cells = TabBarLayout.Layout(labels, 80, GraphGlyphSet.Unicode);

        Assert.Null(TabBarLayout.NumberAt(cells, 0));
        Assert.Equal(1, TabBarLayout.NumberAt(cells, cells[0].Start));
        Assert.Equal(1, TabBarLayout.NumberAt(cells, cells[0].Start + cells[0].Width - 1));
        Assert.Null(TabBarLayout.NumberAt(cells, cells[0].Start + cells[0].Width));
        Assert.Equal(2, TabBarLayout.NumberAt(cells, cells[1].Start));
        Assert.Null(TabBarLayout.NumberAt(cells, 79));
    }

    [Fact]
    public void GivenNoTabsAtAll_WhenRendering_ThenTheRowSaysNoRepositoryIsOpen()
    {
        var model = new ScreenModel(string.Empty, null, 0, 0, "INSERT", string.Empty, 0, 0, false, string.Empty);

        Assert.Equal(" no repository open: Ctrl-t to open one", ChromeBuilder.Tabs(model, 40, GraphGlyphSet.Unicode).Text.TrimEnd());
    }
}
