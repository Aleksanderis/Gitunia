using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Filtering;
using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Filtering;

[Trait("Os", "Independent")]
public class FilterListTests
{
    private const string NoMatch = "No matching thing";

    private static readonly string[] Names = ["main", "feature/login", "feature/logout", "release"];

    [Fact]
    public void GivenAnEmptyQuery_WhenFiltering_ThenEveryItemStaysInItsOrder()
    {
        var list = Of(MatcherKind.Fuzzy, Names);

        Assert.Equal(Names, list.Candidates);
        Assert.Equal("1/4", list.Position);
        Assert.Null(list.EmptyText);
    }

    [Fact]
    public void GivenAFuzzyQuery_WhenFiltering_ThenTheBestMatchComesFirst()
    {
        var list = Of(MatcherKind.Fuzzy, "blueprint-organizer-g", "feature/login").WithField("log");

        Assert.Equal("feature/login", list.Candidates[0]);
        Assert.Equal(["feature/login", "blueprint-organizer-g"], list.Candidates);
    }

    [Fact]
    public void GivenAContainsQuery_WhenFiltering_ThenTheListKeepsItsOrderAndSkipsScatteredMatches()
    {
        var list = Of(MatcherKind.Contains, "login-feature", "lxoxgx", "feature/log").WithField("LOG");

        Assert.Equal(["login-feature", "feature/log"], list.Candidates);
    }

    [Fact]
    public void GivenWhitespaceAroundTheQuery_WhenFiltering_ThenItIsIgnored()
    {
        var list = Of(MatcherKind.Contains, Names).WithField("  release ");

        Assert.Equal(["release"], list.Candidates);
    }

    [Fact]
    public void GivenNothingMatches_WhenAskingForTheEmptyText_ThenItIsTheNoMatchText()
    {
        var list = Of(MatcherKind.Fuzzy, Names).WithField("zzz");

        Assert.Equal(NoMatch, list.EmptyText);
        Assert.Null(list.Position);
        Assert.False(list.TryGetSelected(out _));
    }

    [Fact]
    public void GivenALoadingList_WhenAskingForTheEmptyText_ThenItIsTheLoadingText()
    {
        var list = Of(MatcherKind.Fuzzy, []).Loading();

        Assert.Equal(UiTexts.Loading, list.EmptyText);
        Assert.Equal(FilterStatus.Loading, list.Status);
    }

    [Fact]
    public void GivenAFailedList_WhenAskingForTheEmptyText_ThenItIsTheFailure()
    {
        var list = Of(MatcherKind.Fuzzy, []).Failed("Could not read the branches");

        Assert.Equal("Could not read the branches", list.EmptyText);
    }

    [Fact]
    public void GivenItemsArriveAfterLoading_WhenSettingThem_ThenTheListIsReadyAndFilteredByTheQuery()
    {
        var loading = Of(MatcherKind.Fuzzy, []).WithField("rele").Loading();

        var ready = loading.WithItems([.. Names.Select(name => FilterItems.Of(name, name))]);

        Assert.Equal(FilterStatus.Ready, ready.Status);
        Assert.Equal(["release"], ready.Candidates);
        Assert.Null(ready.EmptyText);
    }

    [Fact]
    public void GivenTheEnds_WhenMoving_ThenTheHighlightStopsThere()
    {
        var list = Of(MatcherKind.Fuzzy, Names);

        Assert.Equal(0, list.Move(-1).Selected);
        Assert.Equal(3, list.Move(100).Selected);
        Assert.Equal(3, list.Move(100).Move(1).Selected);
        Assert.Equal(2, list.MoveTo(2).Selected);
        Assert.Equal(3, list.MoveTo(99).Selected);
    }

    [Fact]
    public void GivenAnEmptyList_WhenMoving_ThenTheHighlightStaysAtZero()
    {
        var list = Of(MatcherKind.Fuzzy, []);

        Assert.Equal(0, list.Move(1).Selected);
        Assert.Equal(0, list.Move(-1).Selected);
    }

    [Fact]
    public void GivenAHighlightedRow_WhenTheTextChanges_ThenTheHighlightGoesBackToTheFirstRow()
    {
        var list = Of(MatcherKind.Contains, Names).Move(2);

        var typed = list.WithField("e");

        Assert.Equal(0, typed.Selected);
    }

    [Fact]
    public void GivenAHighlightedRow_WhenOnlyTheCaretMoves_ThenTheHighlightStays()
    {
        var list = Of(MatcherKind.Contains, Names).WithField("e").Move(2);

        var moved = list.WithField(new TextField("e", 0));

        Assert.Equal(2, moved.Selected);
        Assert.Equal(0, moved.Field.Caret);
    }

    [Fact]
    public void GivenLeadingItems_WhenFiltering_ThenTheyComeBeforeTheMatches()
    {
        var list = Of(MatcherKind.Fuzzy, Names).WithField("rele");

        var withLeading = list.WithLeading(["abc123 a commit"]);

        Assert.Equal(["abc123 a commit", "release"], withLeading.Candidates);
    }

    [Fact]
    public void GivenABonus_WhenTheScoresAreEqual_ThenTheItemWithTheBonusComesFirst()
    {
        ImmutableArray<FilterItem<string>> items = [FilterItems.Of("a", "same"), FilterItems.Of("b", "same", 1)];

        var list = FilterLists.Of(items, MatcherKind.Fuzzy, NoMatch, "same");

        Assert.Equal(["b", "a"], list.Candidates);
        Assert.Equal(["b", "a"], FilterLists.Of(items, MatcherKind.Fuzzy, NoMatch).Candidates);
    }

    [Fact]
    public void GivenAContainsListWithSeveralTexts_WhenFiltering_ThenTheListKeepsItsOrderWhicheverTextMatches()
    {
        ImmutableArray<FilterItem<string>> items =
        [
            FilterItems.Of("second text", "alpha", "needle"),
            FilterItems.Of("first text", "needle", "beta"),
        ];

        var list = FilterLists.Of(items, MatcherKind.Contains, NoMatch, "needle");

        Assert.Equal(["second text", "first text"], list.Candidates);
    }

    [Fact]
    public void GivenALoadingOrFailedList_WhenItHadCandidates_ThenTheyAreCleared()
    {
        var list = Of(MatcherKind.Fuzzy, Names);

        Assert.Empty(list.Loading().Candidates);
        Assert.Empty(list.Failed("x").Candidates);
    }

    [Fact]
    public void GivenAnItemMatchedOnATitleAndAnIdentifier_WhenFiltering_ThenTheTitleOutranksTheIdentifier()
    {
        ImmutableArray<FilterItem<string>> items =
        [
            FilterItems.Of("by id", "Open the thing", "tab.close"),
            FilterItems.Of("by title", "Close the tab", "other"),
        ];

        var list = FilterLists.Of(items, MatcherKind.Fuzzy, NoMatch, "tab");

        Assert.Equal(["by title", "by id"], list.Candidates);
    }

    [Fact]
    public void GivenGroups_WhenFiltering_ThenTheGroupWithTheBestMatchComesFirstAndGroupsStayTogether()
    {
        ImmutableArray<FilterItem<string>> items =
        [
            FilterItems.Of("a1", "xx tag one", 0, "A"),
            FilterItems.Of("b1", "xx tag", 0, "B"),
            FilterItems.Of("a2", "tag", 0, "A"),
            FilterItems.Of("c1", "nothing", 0, "C"),
        ];

        var list = FilterLists.Of(items, MatcherKind.Fuzzy, NoMatch, "tag");

        Assert.Equal(["a2", "a1", "b1"], list.Candidates);
    }

    [Fact]
    public void GivenAViewport_WhenTheHighlightIsFarDown_ThenTheWindowCentresIt()
    {
        var list = Of(MatcherKind.Fuzzy, [.. Enumerable.Range(0, 20).Select(index => $"item{index:D2}")]).MoveTo(10);

        var viewport = list.Viewport(5);

        Assert.Equal(5, viewport.Rows);
        Assert.Equal(8, viewport.Top);
        Assert.True(viewport.IsVisible(10));
    }

    [Fact]
    public void GivenTheSameQuery_WhenScoringByTheTwoMatchers_ThenThereIsOneImplementationOfEach()
    {
        Assert.Equal(FuzzyMatcher.Score("lg", "login"), Matcher.Score(MatcherKind.Fuzzy, "lg", "login"));
        Assert.Equal(ContainsMatcher.Score("lg", "login"), Matcher.Score(MatcherKind.Contains, "lg", "login"));
        Assert.NotNull(Matcher.Score(MatcherKind.Fuzzy, "lg", "login"));
        Assert.Null(Matcher.Score(MatcherKind.Contains, "lg", "login"));
    }

    private static FilterList<string> Of(MatcherKind kind, params string[] names) =>
        FilterLists.Of([.. names.Select(name => FilterItems.Of(name, name))], kind, NoMatch);
}
