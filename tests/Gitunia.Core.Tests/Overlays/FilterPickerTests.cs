using FuzzyMatcher = Gitunia.Core.Search.FuzzyMatcher;
using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

[Trait("Os", "Independent")]
public class FilterPickerTests
{
    private static FilterPicker<string> Picker(string query, params string[] names)
    {
        var picker = new FilterPicker<string>();
        picker.Filter(names, query, name => name, FuzzyMatcher.Score);
        return picker;
    }

    [Fact]
    public void GivenAnEmptyQuery_WhenListing_ThenEveryCandidateIsVisibleAndTheFirstIsHighlighted()
    {
        var picker = Picker(string.Empty, "origin", "backup");

        var rows = picker.Rows(name => name, "none");

        Assert.Equal(["> origin", "  backup", "(1/2)"], rows);
    }

    [Fact]
    public void GivenAQuery_WhenFiltering_ThenOnlyMatchesRemain()
    {
        var picker = Picker("back", "origin", "backup");

        Assert.Equal(["backup"], picker.Candidates);
    }

    [Fact]
    public void GivenNoMatch_WhenListing_ThenOnlyTheNoneRowShowsAndNothingIsSelected()
    {
        var picker = Picker("zzz", "origin");

        Assert.Equal(["no match"], picker.Rows(name => name, "no match"));
        Assert.False(picker.TryGetSelected(out _));
    }

    [Theory]
    [InlineData(1, "backup")]
    [InlineData(2, "origin")]
    [InlineData(-1, "backup")]
    public void GivenTwoCandidates_WhenMoving_ThenTheHighlightWraps(int step, string expected)
    {
        var picker = Picker(string.Empty, "origin", "backup");

        picker.Move(step);

        Assert.True(picker.TryGetSelected(out var selected));
        Assert.Equal(expected, selected);
    }

    [Fact]
    public void GivenManyCandidates_WhenHighlightingTheLast_ThenTheWindowEndsWithIt()
    {
        var names = Enumerable.Range(0, 20).Select(number => $"r{number:D2}").ToArray();
        var picker = Picker(string.Empty, names);

        picker.Move(-1);
        var rows = picker.Rows(name => name, "none");

        Assert.Equal(PickerRows.VisibleRows + 1, rows.Count);
        Assert.Equal("> r19", rows[^2]);
        Assert.Equal("(20/20)", rows[^1]);
    }

    [Fact]
    public void GivenAFilterPopup_WhenBuilt_ThenTheHintIsTheOneFilterPickerLine()
    {
        var popup = Picker(string.Empty, "origin").ToPopup("Pick", "Remote", "q", "choose", name => name, "none");

        Assert.Equal("Type filters   Up/Down select   Enter choose   Esc cancel", popup.Hint);
        Assert.Equal("q", popup.Input);
    }
}
