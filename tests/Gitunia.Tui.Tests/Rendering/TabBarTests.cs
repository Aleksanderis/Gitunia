using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class TabBarTests
{
    private static TabLabel Label(int number, string name, bool isActive = false, string? branch = "main", bool dirty = false, int ahead = 0, int behind = 0, bool missing = false, bool loading = false) =>
        new(number, name, branch, isActive, dirty, ahead, behind, missing, loading);

    private static ScreenModel Model(params TabLabel[] labels) =>
        new("repo", "main", 1, labels.Length, "NORMAL", string.Empty, 0, 0, false, string.Empty, labels);

    private static string Row(int width, GraphGlyphSet glyphs, params TabLabel[] labels) =>
        ChromeBuilder.Tabs(Model(labels), width, glyphs).Text.TrimEnd();

    [Fact]
    public void GivenTabsWithMarkers_WhenRendering_ThenNameBranchAndMarkersAreShown()
    {
        var row = Row(
            80,
            GraphGlyphSet.Unicode,
            Label(1, "alpha", isActive: true),
            Label(2, "beta", dirty: true, ahead: 2, behind: 1),
            Label(3, "gamma", branch: null));

        Assert.Equal(" 1 alpha  main   2 beta  main *↑2↓1   3 gamma", row);
    }

    [Fact]
    public void GivenTabsWithoutUnicode_WhenRendering_ThenAheadAndBehindUsePlusAndMinus()
    {
        var row = Row(80, GraphGlyphSet.Ascii, Label(1, "alpha", isActive: true, dirty: true, ahead: 3, behind: 4));

        Assert.Equal(" 1 alpha  main *+3-4", row);
    }

    [Fact]
    public void GivenMissingTab_WhenRendering_ThenItIsMarkedAndShowsNoBranch()
    {
        var row = Row(80, GraphGlyphSet.Unicode, Label(1, "alpha", missing: true, branch: null), Label(2, "beta", isActive: true));

        Assert.Equal(" 1 ! alpha (missing)   2 beta  main", row);
    }

    [Fact]
    public void GivenLoadingTab_WhenRendering_ThenTheBackgroundActivityMarkerIsShown()
    {
        var row = Row(80, GraphGlyphSet.Unicode, Label(1, "alpha", isActive: true, loading: true));

        Assert.Equal(" 1 alpha  main …", row);
    }

    [Fact]
    public void GivenMoreTabsThanFit_WhenTheLastIsActive_ThenTheRowScrollsToKeepItVisible()
    {
        var labels = Enumerable.Range(1, 8).Select(number => Label(number, "repo" + number, isActive: number == 8)).ToArray();

        var row = Row(40, GraphGlyphSet.Unicode, labels);

        Assert.StartsWith("< ", row, StringComparison.Ordinal);
        Assert.Contains("8 repo8  main", row, StringComparison.Ordinal);
        Assert.DoesNotContain(" 1 repo1", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMoreTabsThanFit_WhenTheFirstIsActive_ThenTheRowStartsAtTheFirstTab()
    {
        var labels = Enumerable.Range(1, 8).Select(number => Label(number, "repo" + number, isActive: number == 1)).ToArray();

        var row = Row(40, GraphGlyphSet.Unicode, labels);

        Assert.StartsWith(" 1 repo1", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTabRow_WhenHitTestingColumns_ThenTheTabUnderTheColumnIsFoundAndGapsAreNot()
    {
        var labels = new[] { Label(1, "alpha", isActive: true), Label(2, "beta") };
        var cells = TabBarLayout.Layout(labels, 80, GraphGlyphSet.Unicode);

        Assert.Equal(1, TabBarLayout.NumberAt(cells, 0));
        Assert.Equal(1, TabBarLayout.NumberAt(cells, cells[0].Width - 1));
        Assert.Null(TabBarLayout.NumberAt(cells, cells[0].Width));
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
