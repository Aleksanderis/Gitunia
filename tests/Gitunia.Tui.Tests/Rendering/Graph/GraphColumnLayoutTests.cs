using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Graph;

public class GraphColumnLayoutTests
{
    private const int AuthorShown = 1;
    private const int DateShown = 2;
    private const int HashShown = 4;

    [Theory]
    [InlineData(80, 2, 16, 33)]
    [InlineData(120, 2, 16, 65)]
    [InlineData(200, 2, 20, 141)]
    [InlineData(400, 2, 20, 341)]
    public void GivenWideEnoughTerminal_WhenComputing_ThenAllColumnsShowAndMessageTakesTheRest(int width, int laneColumns, int author, int message)
    {
        var columns = GraphColumnLayout.Compute(width, laneColumns);
        var isSpacious = width - laneColumns * 2 - 1 >= GraphColumnLayout.SpaciousRowWidth;
        var gap = isSpacious ? GraphColumnLayout.SpaciousColumnGap : GraphColumnLayout.CompactColumnGap;
        var margin = isSpacious ? GraphColumnLayout.SpaciousRightMargin : 0;

        Assert.Equal(new GraphColumns(laneColumns * 2, message, author, GraphColumnLayout.DateWidth, GraphColumnLayout.HashWidth, gap, margin), columns);
    }

    [Theory]
    [InlineData(75, 7, 16, 16)]
    [InlineData(70, 0, 16, 16)]
    [InlineData(65, 0, 10, 16)]
    [InlineData(60, 0, 0, 16)]
    [InlineData(49, 0, 0, 0)]
    public void GivenNarrowTerminal_WhenComputing_ThenHashGoesFirstThenTheTimeThenTheDateThenAuthor(int width, int hash, int date, int author)
    {
        var columns = GraphColumnLayout.Compute(width, 1);

        Assert.Equal(hash, columns.HashWidth);
        Assert.Equal(author, columns.AuthorWidth);
        Assert.Equal(date, columns.DateWidth);
        Assert.True(author == 0 || columns.MessageWidth >= GraphColumnLayout.MinimumMessageWidth);
    }

    [Theory]
    [InlineData(1, 139, 16, 89)]
    [InlineData(1, 140, 20, 86)]
    [InlineData(3, 139, 16, 89)]
    [InlineData(3, 140, 20, 86)]
    public void GivenSpaceAfterTheLanes_WhenComputing_ThenAuthorWidensAtTheThresholdOnly(int laneColumns, int available, int author, int message)
    {
        var columns = GraphColumnLayout.Compute(available + laneColumns * 2 + 1, laneColumns);

        Assert.Equal(author, columns.AuthorWidth);
        Assert.Equal(message, columns.MessageWidth);
    }

    [Fact]
    public void GivenGrowingWidth_WhenComputing_ThenColumnsComeBackInOrderAndAuthorWidensOnWideRows()
    {
        var all = Enumerable.Range(10, 391).Select(width => GraphColumnLayout.Compute(width, 1)).ToArray();
        var shown = all
            .Select(ShownMask)
            .ToArray();

        Assert.Equal(shown.Order(), shown);
        Assert.All(shown, mask => Assert.True(mask is 0 or 1 or 3 or 7, $"mask {mask}"));
        Assert.Equal(GraphColumnLayout.WideAuthorWidth, all[^1].AuthorWidth);
        Assert.Equal(343, all[^1].MessageWidth);
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
            + (columns.AuthorWidth > 0 ? columns.ColumnGap + columns.AuthorWidth : 0)
            + (columns.DateWidth > 0 ? columns.ColumnGap + columns.DateWidth : 0)
            + (columns.HashWidth > 0 ? columns.ColumnGap + columns.HashWidth : 0)
            + (columns.HashWidth > 0 ? columns.RightMargin : 0);

        Assert.True(columns.MessageWidth >= 0);
        Assert.True(used <= Math.Max(width, columns.LaneWidth + 1), $"{used} > {width}");
    }

    [Theory]
    [InlineData(80)]
    [InlineData(100)]
    public void GivenCompactWidth_WhenComputing_ThenGapsStayOneColumnWithNoMargin(int width)
    {
        var columns = GraphColumnLayout.Compute(width, 2);

        Assert.Equal(GraphColumnLayout.CompactColumnGap, columns.ColumnGap);
        Assert.Equal(0, columns.RightMargin);
    }

    [Theory]
    [InlineData(160)]
    [InlineData(200)]
    public void GivenWideWidth_WhenComputing_ThenGapsAndRightMarginGrow(int width)
    {
        var columns = GraphColumnLayout.Compute(width, 2);

        Assert.Equal(GraphColumnLayout.SpaciousColumnGap, columns.ColumnGap);
        Assert.Equal(GraphColumnLayout.SpaciousRightMargin, columns.RightMargin);
    }

    [Theory]
    [InlineData(99, 1, 0)]
    [InlineData(100, 3, 2)]
    public void GivenRowWidthAroundTheThreshold_WhenComputing_ThenGapsSwitchExactlyAtOneHundred(int available, int gap, int margin)
    {
        var columns = GraphColumnLayout.Compute(available + 1 + 2, 1);

        Assert.Equal(gap, columns.ColumnGap);
        Assert.Equal(margin, columns.RightMargin);
    }

    private static int ShownMask(GraphColumns columns)
    {
        var mask = 0;

        if (columns.AuthorWidth > 0)
        {
            mask |= AuthorShown;
        }

        if (columns.DateWidth > 0)
        {
            mask |= DateShown;
        }

        if (columns.HashWidth > 0)
        {
            mask |= HashShown;
        }

        return mask;
    }
}
