using Gitunia.Core.Lists;
using Xunit;

namespace Gitunia.Core.Tests.Lists;

[Trait("Os", "Independent")]
public class RowRangeTests
{
    [Fact]
    public void GivenTheCursorBelowTheAnchor_WhenAskingForTheBounds_ThenTheAnchorIsFirst()
    {
        var range = new RowRange(3, 7);

        Assert.Equal(3, range.First);
        Assert.Equal(7, range.Last);
        Assert.Equal(5, range.Count);
    }

    [Fact]
    public void GivenTheCursorAboveTheAnchor_WhenAskingForTheBounds_ThenTheCursorIsFirst()
    {
        var range = new RowRange(7, 3);

        Assert.Equal(3, range.First);
        Assert.Equal(7, range.Last);
        Assert.True(range.Contains(5));
    }

    [Fact]
    public void GivenARange_WhenCheckingRowsOutsideIt_ThenTheyAreNotContained()
    {
        var range = new RowRange(3, 7);

        Assert.False(range.Contains(2));
        Assert.False(range.Contains(8));
        Assert.True(range.Contains(3));
        Assert.True(range.Contains(7));
    }

    [Fact]
    public void GivenAFreshRange_WhenCountingRows_ThenItCoversTheAnchorRowOnly()
    {
        var range = new RowRange(4, 4);

        Assert.Equal(1, range.Count);
        Assert.True(range.Contains(4));
    }

    [Fact]
    public void GivenARange_WhenMovingTheCursor_ThenTheAnchorStays()
    {
        var moved = new RowRange(4, 4).MoveCursorTo(1);

        Assert.Equal(4, moved.Anchor);
        Assert.Equal(1, moved.First);
        Assert.Equal(4, moved.Count);
    }
}
