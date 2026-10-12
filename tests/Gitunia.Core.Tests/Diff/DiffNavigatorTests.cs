using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

public class DiffNavigatorTests
{
    private static readonly int[] Anchors = [2, 10, 30];

    [Theory]
    [InlineData(0, true, 1, 2)]
    [InlineData(2, true, 1, 10)]
    [InlineData(10, true, 5, 30)]
    [InlineData(5, true, 2, 30)]
    [InlineData(30, false, 1, 10)]
    [InlineData(11, false, 1, 10)]
    [InlineData(100, false, 2, 10)]
    [InlineData(100, false, 9, 2)]
    public void GivenARow_WhenStepping_ThenTheAnchorIsFound(int row, bool forward, int count, int expected)
    {
        Assert.Equal(expected, DiffNavigator.Step(Anchors, row, forward, count));
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(2, false)]
    [InlineData(0, false)]
    public void GivenNoAnchorInThatDirection_WhenStepping_ThenThereIsNoTarget(int row, bool forward)
    {
        Assert.Null(DiffNavigator.Step(Anchors, row, forward, 1));
    }

    [Fact]
    public void GivenNoAnchors_WhenStepping_ThenThereIsNoTarget()
    {
        Assert.Null(DiffNavigator.Step([], 0, true, 1));
    }
}
