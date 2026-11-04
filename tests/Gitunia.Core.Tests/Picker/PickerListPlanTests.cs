using System.Collections.Immutable;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

public class PickerListPlanTests
{
    private const int TallBudget = PickerListPlan.MaximumListRows;

    [Fact]
    public void GivenMoreThanFiveRecents_WhenBuildingTheList_ThenFiveAreShownWithAMoreLineForTheRest()
    {
        var state = Opened(openCount: 1, pinnedCount: 1, recentCount: 31, foundCount: 5);

        var entries = PickerListPlan.Build(state, TallBudget);

        Assert.Equal(5, RowsOf(entries, PickerSection.Recent));
        var more = Assert.Single(entries, entry => entry.Kind == PickerEntryKind.More && entry.Section == PickerSection.Recent);
        Assert.Equal(26, more.Hidden);
        var header = Assert.Single(entries, entry => entry.Kind == PickerEntryKind.Header && entry.Section == PickerSection.Recent);
        Assert.Equal((5, 31), (header.Shown, header.Total));
    }

    [Fact]
    public void GivenSectionsWithinTheirCaps_WhenBuildingTheList_ThenNothingIsHiddenAndThereIsNoMoreLine()
    {
        var state = Opened(openCount: 2, pinnedCount: 1, recentCount: 5, foundCount: 3);

        var entries = PickerListPlan.Build(state, TallBudget);

        Assert.DoesNotContain(entries, entry => entry.IsToggle);
        Assert.Equal(state.Rows.Length, entries.Count(entry => entry.Kind == PickerEntryKind.Row));
    }

    [Fact]
    public void GivenCapsThatDoNotFit_WhenBuildingAtTheTallestBudget_ThenFoundGivesUpRowsFirstAndRecentLast()
    {
        var state = Opened(openCount: 3, pinnedCount: 2, recentCount: 31, foundCount: 20);

        var entries = PickerListPlan.Build(state, TallBudget);

        Assert.Equal(TallBudget, entries.Length);
        Assert.Equal(2, RowsOf(entries, PickerSection.Found));
        Assert.Equal(2, RowsOf(entries, PickerSection.Pinned));
        Assert.Equal(3, RowsOf(entries, PickerSection.Open));
        Assert.Equal(4, RowsOf(entries, PickerSection.Recent));
    }

    [Fact]
    public void GivenACommonFourSectionState_WhenBuildingAtTheTallestBudget_ThenFiveRecentsAreStillShown()
    {
        var state = Opened(openCount: 2, pinnedCount: 1, recentCount: 31, foundCount: 20);

        var entries = PickerListPlan.Build(state, TallBudget);

        Assert.Equal(5, RowsOf(entries, PickerSection.Recent));
        Assert.True(entries.Length <= TallBudget);
    }

    [Fact]
    public void GivenASectionShrunkBelowItsCap_WhenExpanded_ThenALessLineStillCollapsesIt()
    {
        var state = Opened(openCount: 3, pinnedCount: 2, recentCount: 5, foundCount: 20);
        var tight = PickerListPlan.Build(state, 11);
        Assert.True(RowsOf(tight, PickerSection.Recent) < 5);
        var onMore = state with { Highlight = tight.Last(entry => entry.Kind == PickerEntryKind.More && entry.Section == PickerSection.Recent).RowIndex, IsOnToggle = true };

        var expanded = PickerListPlan.Toggle(onMore, 11);

        var entries = PickerListPlan.Build(expanded, 11);
        Assert.Contains(entries, entry => entry.Kind == PickerEntryKind.Less && entry.Section == PickerSection.Recent);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(18)]
    public void GivenAnyBudget_WhenBuildingTheList_ThenEverySectionKeepsTwoRowsAndItsMoreLine(int budget)
    {
        var state = Opened(openCount: 8, pinnedCount: 8, recentCount: 31, foundCount: 20);

        var entries = PickerListPlan.Build(state, budget);

        foreach (var section in new[] { PickerSection.Open, PickerSection.Pinned, PickerSection.Recent, PickerSection.Found })
        {
            Assert.True(RowsOf(entries, section) >= PickerListPlan.MinimumShown, $"{section} at {budget}");
            Assert.Contains(entries, entry => entry.Kind == PickerEntryKind.More && entry.Section == section);
        }
    }

    [Fact]
    public void GivenARoomyBudget_WhenBuildingTheList_ThenTheCapsAreTheNominalOnes()
    {
        var state = Opened(openCount: 8, pinnedCount: 8, recentCount: 31, foundCount: 20);

        var entries = PickerListPlan.Build(state, 40);

        Assert.Equal(6, RowsOf(entries, PickerSection.Open));
        Assert.Equal(6, RowsOf(entries, PickerSection.Pinned));
        Assert.Equal(5, RowsOf(entries, PickerSection.Recent));
        Assert.Equal(3, RowsOf(entries, PickerSection.Found));
    }

    [Fact]
    public void GivenAQuery_WhenBuildingTheList_ThenCapsAndHeadersAreIgnoredAndAllMatchesAreRows()
    {
        var state = Opened(openCount: 0, pinnedCount: 0, recentCount: 31, foundCount: 0);
        var context = ContextOf(0, 0, 31, 0);

        var typed = PickerReducer.Reduce(state, new TypeTextCommand("repo"), context);
        var entries = PickerListPlan.Build(typed, 4);

        Assert.All(entries, entry => Assert.Equal(PickerEntryKind.Row, entry.Kind));
        Assert.Equal(typed.Rows.Length, entries.Length);
    }

    [Fact]
    public void GivenTheHighlightAtTheLastShownRecent_WhenMovingDown_ThenItLandsOnTheMoreLineThenOnTheNextSection()
    {
        var state = Opened(openCount: 0, pinnedCount: 0, recentCount: 31, foundCount: 4);
        state = state with { Highlight = 4 };

        var onMore = PickerListPlan.Move(state, 1, TallBudget);
        var onFound = PickerListPlan.Move(onMore, 1, TallBudget);

        Assert.True(onMore.IsOnToggle);
        Assert.Equal(4, onMore.Highlight);
        Assert.Null(onMore.HighlightedRow);
        Assert.False(onFound.IsOnToggle);
        Assert.Equal(31, onFound.Highlight);
        Assert.Equal(onMore, PickerListPlan.Move(onFound, -1, TallBudget));
    }

    [Fact]
    public void GivenTheHighlightOnAMoreLine_WhenToggling_ThenTheSectionExpandsAndTheHighlightLandsOnTheFirstNewRow()
    {
        var state = OnMore(Opened(openCount: 0, pinnedCount: 0, recentCount: 31, foundCount: 4));

        var expanded = PickerListPlan.Toggle(state, TallBudget);

        var entries = PickerListPlan.Build(expanded, TallBudget);
        Assert.Equal(31, RowsOf(entries, PickerSection.Recent));
        Assert.Equal(PickerEntryKind.Less, entries.Single(entry => entry.IsToggle && entry.Section == PickerSection.Recent).Kind);
        Assert.Equal(5, expanded.Highlight);
        Assert.False(expanded.IsOnToggle);
    }

    [Fact]
    public void GivenAnExpandedSection_WhenTogglingTheLessLine_ThenItCollapsesAndTheHighlightSitsOnTheMoreLine()
    {
        var expanded = PickerListPlan.Toggle(OnMore(Opened(0, 0, 31, 4)), TallBudget);
        var onLess = PickerListPlan.Move(expanded with { Highlight = 30 }, 1, TallBudget);
        Assert.True(onLess.IsOnToggle);

        var collapsed = PickerListPlan.Toggle(onLess, TallBudget);

        Assert.Equal(PickerSection.None, collapsed.Expanded);
        Assert.True(collapsed.IsOnToggle);
        var entries = PickerListPlan.Build(collapsed, TallBudget);
        Assert.Equal(PickerEntryKind.More, entries[PickerListPlan.PositionOf(entries, collapsed)].Kind);
    }

    [Fact]
    public void GivenAHighlightedRowThatASmallerListHides_WhenNormalizing_ThenTheHighlightMovesToTheMoreLine()
    {
        var state = Opened(openCount: 0, pinnedCount: 0, recentCount: 31, foundCount: 4) with { Highlight = 20 };

        var normalized = PickerListPlan.Normalize(state, TallBudget);

        Assert.True(normalized.IsOnToggle);
        Assert.Equal(4, normalized.Highlight);
    }

    [Fact]
    public void GivenAnExpandedSectionAndATypedQuery_WhenTheQueryIsCleared_ThenTheExpansionIsStillThere()
    {
        var context = ContextOf(0, 0, 31, 4);
        var expanded = PickerListPlan.Toggle(OnMore(Opened(0, 0, 31, 4)), TallBudget);

        var typed = PickerReducer.Reduce(expanded, new TypeTextCommand("re"), context);
        var cleared = PickerReducer.Reduce(typed, new ClearQueryCommand(), context);

        Assert.Equal(PickerSection.Recent, cleared.Expanded);
    }

    private static PickerState OnMore(PickerState state) =>
        PickerListPlan.Move(state with { Highlight = 4 }, 1, TallBudget);

    private static int RowsOf(IEnumerable<PickerListEntry> entries, PickerSection section) =>
        entries.Count(entry => entry.Kind == PickerEntryKind.Row && entry.Section == section);

    private static PickerState Opened(int openCount, int pinnedCount, int recentCount, int foundCount) =>
        PickerReducer.Open(ContextOf(openCount, pinnedCount, recentCount, foundCount));

    public static PickerContext ContextOf(int openCount, int pinnedCount, int recentCount, int foundCount)
    {
        var workspace = PickerFixtures.Open(Workspace.Empty, [.. Names("open", openCount)]);
        workspace = workspace with
        {
            Pins = [.. Names("pinned", pinnedCount).Select(name => PickerFixtures.PathOf("code", name))],
            Recents = [.. Names("repo", recentCount).Select(name => PickerFixtures.PathOf("code", name))],
        };
        ImmutableArray<string> found = [.. Names("found", foundCount).Select(name => PickerFixtures.PathOf("code", name))];

        return new PickerContext(workspace, found, PickerFixtures.PathOf("code", "current"));
    }

    private static IEnumerable<string> Names(string prefix, int count) =>
        Enumerable.Range(1, count).Select(number => $"{prefix}{number:00}");
}
