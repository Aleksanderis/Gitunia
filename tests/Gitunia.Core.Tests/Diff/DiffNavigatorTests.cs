using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

[Trait("Os", "Independent")]
public class DiffNavigatorTests
{
    private static readonly int[] Anchors = [2, 10, 30];

    [Fact]
    public void GivenARow_WhenStepping_ThenTheAnchorIsFound()
    {
        var failures = new List<string>();
        (int row, bool forward, int count, int expected)[] rows =
        [
            (0, true, 1, 2),
            (2, true, 1, 10),
            (10, true, 5, 30),
            (5, true, 2, 30),
            (30, false, 1, 10),
            (11, false, 1, 10),
            (100, false, 2, 10),
            (100, false, 9, 2),
        ];

        foreach (var (row, forward, count, expected) in rows)
        {
            try
            {
                CheckTheAnchorIsFound(row, forward, count, expected);
            }
            catch (Exception exception)
            {
                failures.Add($"{row}, {forward}, {count}, {expected}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckTheAnchorIsFound(int row, bool forward, int count, int expected)
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
