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

    private static ReflogState State(int count) => new(
        new TabId(1),
        "HEAD",
        "HEAD",
        [.. Enumerable.Range(0, count).Select(index => new ReflogEntry(index, "abc", "commit", "m", "now"))]);
}
