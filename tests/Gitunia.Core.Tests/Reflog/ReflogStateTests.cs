using Gitunia.Core.Reflog;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Reflog.Models;
using Xunit;

namespace Gitunia.Core.Tests.Reflog;

public class ReflogStateTests
{
    [Fact]
    public void GivenEntries_WhenMovingPastTheEnds_ThenTheHighlightIsClamped()
    {
        var state = State(3);

        Assert.Equal(2, state.MoveTo(10).SelectedIndex);
        Assert.Equal(0, state.MoveTo(-4).SelectedIndex);
        Assert.Equal(1, state.MoveTo(1).Selected!.Index);
    }

    [Fact]
    public void GivenNoEntries_WhenMoving_ThenNothingIsSelected()
    {
        var state = State(0).MoveTo(3);

        Assert.Equal(0, state.SelectedIndex);
        Assert.Null(state.Selected);
    }

    [Fact]
    public void GivenAShorterReread_WhenReplacingTheEntries_ThenTheHighlightStaysInsideTheList()
    {
        var state = State(5).MoveTo(4).WithEntries(State(2).Entries);

        Assert.Equal(1, state.SelectedIndex);
        Assert.Equal(2, state.Entries.Count);
        Assert.Equal(0, state.WithEntries([]).SelectedIndex);
    }

    [Fact]
    public void GivenASearch_WhenTyping_ThenTheHighlightJumpsToTheFirstMatchAtOrAfterIt()
    {
        var state = State(6).MoveTo(2).OpenSearch().EditSearch("message 4");

        Assert.True(state.IsEditingSearch);
        Assert.Equal([4], state.MatchIndexes);
        Assert.Equal(4, state.SelectedIndex);
    }

    [Fact]
    public void GivenSeveralMatches_WhenSteppingBothWays_ThenTheSearchWraps()
    {
        var state = State(6).OpenSearch().EditSearch("MESSAGE").AcceptSearch().MoveTo(4);

        Assert.Equal(5, state.StepMatch(true, 1).SelectedIndex);
        Assert.Equal(0, state.StepMatch(true, 2).SelectedIndex);
        Assert.Equal(3, state.StepMatch(false, 1).SelectedIndex);
        Assert.Equal(5, state.MoveTo(0).StepMatch(false, 1).SelectedIndex);
        Assert.False(state.IsEditingSearch);
    }

    [Fact]
    public void GivenNoMatch_WhenSearching_ThenTheHighlightStaysAndClearingDropsTheSearch()
    {
        var state = State(3).MoveTo(1).OpenSearch().EditSearch("zzz");

        Assert.Empty(state.MatchIndexes);
        Assert.Equal(1, state.SelectedIndex);
        Assert.False(state.ClearSearch().HasSearch);
    }

    [Fact]
    public void GivenTheSelector_WhenSearchingForIt_ThenItMatches()
    {
        Assert.Equal([2], State(4).OpenSearch().EditSearch("HEAD@{2}").MatchIndexes);
    }

    [Fact]
    public void GivenTheRefPicker_WhenFilteringAndMoving_ThenOnlyMatchingNamesAreChosen()
    {
        var picker = new ReflogRefPicker(["HEAD", "main", "feature/x"]);

        Assert.Equal("HEAD", picker.Selected);
        Assert.Equal("feature/x", picker.Move(-1).Selected);
        Assert.Equal(["main"], picker.WithText("MAI").Candidates);
        Assert.Null(picker.WithText("zzz").Selected);
        Assert.Equal("main", picker.MoveTo(9).Move(-1).Selected);
    }

    private static ReflogState State(int count) => new(
        new TabId(1),
        "HEAD",
        "HEAD",
        [.. Enumerable.Range(0, count).Select(index =>
            new ReflogEntry(index, "abc", "commit", $"message {index}", "now"))]);
}
