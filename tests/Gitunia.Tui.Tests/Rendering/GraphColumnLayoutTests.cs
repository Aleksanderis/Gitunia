using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class GraphColumnLayoutTests
{
    [Theory]
    [InlineData(80, 2, 39)]
    [InlineData(120, 2, 79)]
    [InlineData(200, 2, 100)]
    public void GivenWideEnoughTerminal_WhenComputing_ThenAllColumnsShowAndMessageStopsAtItsCap(int width, int laneColumns, int message)
    {
        var columns = GraphColumnLayout.Compute(width, laneColumns);

        Assert.Equal(new GraphColumns(laneColumns * 2, message, GraphColumnLayout.AuthorWidth, GraphColumnLayout.DateWidth, GraphColumnLayout.HashWidth), columns);
    }

    [Theory]
    [InlineData(70, 7, 10, 16)]
    [InlineData(60, 0, 0, 16)]
    [InlineData(49, 0, 0, 0)]
    public void GivenNarrowTerminal_WhenComputing_ThenHashGoesFirstThenDateThenAuthor(int width, int hash, int date, int author)
    {
        var columns = GraphColumnLayout.Compute(width, 1);

        Assert.Equal(hash, columns.HashWidth);
        Assert.Equal(author, columns.AuthorWidth);
        Assert.Equal(date, columns.DateWidth);
        Assert.True(author == 0 || columns.MessageWidth >= GraphColumnLayout.MinimumMessageWidth);
    }

    [Fact]
    public void GivenGrowingWidth_WhenComputing_ThenColumnsOnlyComeBackInOrder()
    {
        var shown = Enumerable.Range(10, 191)
            .Select(width => GraphColumnLayout.Compute(width, 1))
            .Select(columns => (columns.AuthorWidth > 0 ? 1 : 0) + (columns.DateWidth > 0 ? 2 : 0) + (columns.HashWidth > 0 ? 4 : 0))
            .ToArray();

        Assert.Equal(shown.Order(), shown);
        Assert.All(shown, mask => Assert.True(mask is 0 or 1 or 3 or 7, $"mask {mask}"));
        Assert.Equal(0, shown[0]);
        Assert.Equal(7, shown[^1]);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(10, 3)]
    [InlineData(20, 4)]
    [InlineData(80, 16)]
    [InlineData(300, 4)]
    public void GivenAnyWidth_WhenComputing_ThenTheColumnsNeverExceedTheRow(int width, int laneColumns)
    {
        var columns = GraphColumnLayout.Compute(width, laneColumns);
        var used = columns.LaneWidth + 1 + columns.MessageWidth
            + (columns.AuthorWidth > 0 ? 1 + columns.AuthorWidth : 0)
            + (columns.DateWidth > 0 ? 1 + columns.DateWidth : 0)
            + (columns.HashWidth > 0 ? 1 + columns.HashWidth : 0);

        Assert.True(columns.MessageWidth >= 0);
        Assert.True(used <= Math.Max(width, columns.LaneWidth + 1), $"{used} > {width}");
    }
}
