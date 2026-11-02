using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

[Trait("Os", "Independent")]
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
    [InlineData(new[] { 2, 10, 30 }, 30, true)]
    [InlineData(new[] { 2, 10, 30 }, 2, false)]
    [InlineData(new[] { 2, 10, 30 }, 0, false)]
    [InlineData(new int[0], 0, true)]
    public void GivenNoAnchorInThatDirection_WhenStepping_ThenThereIsNoTarget(int[] anchors, int row, bool forward)
    {
        Assert.Null(DiffNavigator.Step(anchors, row, forward, 1));
    }
}
