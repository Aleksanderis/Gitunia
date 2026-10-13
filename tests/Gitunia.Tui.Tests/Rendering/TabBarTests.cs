using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

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
    public void GivenLoadingTab_WhenRendering_ThenTheBackgroundActivityMarkerIsShown()
    {
        var row = Row(80, GraphGlyphSet.Unicode, Label(1, "alpha", isActive: true, branch: null, loading: true));

        Assert.StartsWith("   1 alpha …  ", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheActiveAndAnInactiveTab_WhenRendering_ThenOnlyTheActiveOneIsTheChipAndTheRestIsOnTheBar()
    {
        var line = ChromeBuilder.Tabs(Model(Label(1, "alpha", isActive: true, branch: null), Label(2, "beta", branch: null)), 80, GraphGlyphSet.Unicode);

        Assert.Single(line.Segments, segment => segment.Role == SegmentRole.ActiveTab);
        Assert.Equal("  1 alpha  ", line.Segments.Single(segment => segment.Role == SegmentRole.ActiveTab).Text);
        Assert.DoesNotContain(line.Segments, segment => segment.Role == SegmentRole.InactiveTab);
        Assert.Equal(80, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenAWideRow_WhenRendering_ThenTheCommandsButtonEndsTheRowAndNarrowRowsHaveNone()
    {
        var wide = ChromeBuilder.Tabs(Model(Label(1, "alpha", isActive: true, branch: null)), 80, GraphGlyphSet.Unicode);
        var narrow = ChromeBuilder.Tabs(Model(Label(1, "alpha", isActive: true, branch: null)), 40, GraphGlyphSet.Unicode);

        Assert.Equal(TabBarLayout.CommandsLabel, wide.Segments.Single(segment => segment.Role == SegmentRole.BarButton).Text);
        Assert.EndsWith(TabBarLayout.CommandsLabel + " ", wide.Text, StringComparison.Ordinal);
        Assert.True(TabBarLayout.IsOnCommands(80, 70));
        Assert.False(TabBarLayout.IsOnCommands(80, 10));
        Assert.DoesNotContain(narrow.Segments, segment => segment.Role == SegmentRole.BarButton);
        Assert.False(TabBarLayout.IsOnCommands(40, 35));
    }

    [Fact]
    public void GivenMoreTabsThanFit_WhenTheLastIsActive_ThenTheRowScrollsToKeepItVisible()
    {
        var labels = Enumerable.Range(1, 8).Select(number => Label(number, "repo" + number, isActive: number == 8, branch: null)).ToArray();

        var row = Row(60, GraphGlyphSet.Unicode, labels);

        Assert.StartsWith(" < ", row, StringComparison.Ordinal);
        Assert.Contains("8 repo8", row, StringComparison.Ordinal);
        Assert.DoesNotContain(" 1 repo1", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMoreTabsThanFit_WhenTheFirstIsActive_ThenTheRowStartsAtTheFirstTab()
    {
        var labels = Enumerable.Range(1, 8).Select(number => Label(number, "repo" + number, isActive: number == 1, branch: null)).ToArray();

        var row = Row(60, GraphGlyphSet.Unicode, labels);

        Assert.StartsWith("   1 repo1", row, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void GivenTooManyTabs_WhenLayingOut_ThenNoTabReachesTheCommandsButtonAndEveryColumnHasOneOwner(int width)
    {
        var labels = Enumerable.Range(1, 14).Select(number => Label(number, "repo" + number, isActive: number == 1, branch: null)).ToArray();

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
        var name = new string('x', RepoState40());
        var cells = TabBarLayout.Layout([Label(1, name, isActive: true, branch: null)], 60, GraphGlyphSet.Unicode);

        Assert.Equal(1, Assert.Single(cells).Number);
        Assert.True(cells[0].Start + cells[0].Width < TabBarLayout.CommandsStart(60)!.Value);
    }

    private static int RepoState40() => Gitunia.Core.Workspaces.RepoState.MaximumNameLength;

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

        Assert.Equal(" no repository open", ChromeBuilder.Tabs(model, 40, GraphGlyphSet.Unicode).Text.TrimEnd());
    }
}
