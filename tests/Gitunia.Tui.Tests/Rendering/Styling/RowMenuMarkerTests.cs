using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

[Trait("Os", "Independent")]
public class RowMenuMarkerTests
{
    private const int Width = 30;

    [Fact]
    public void GivenTheCursorRow_WhenApplying_ThenTheLineEndsWithTheMarkerAndKeepsItsWidth()
    {
        var line = Line("main branch with a long label that runs to the edge");

        var marked = RowMenuMarker.Apply(line, Width, isCursor: true);

        Assert.EndsWith("...", marked.Text);
        Assert.Equal(Width, TextWidth.Of(marked.Text));
    }

    [Fact]
    public void GivenAShortCursorRow_WhenApplying_ThenTheGapIsPaddedAndTheMarkerStaysAtTheEdge()
    {
        var marked = RowMenuMarker.Apply(Line("main"), Width, isCursor: true);

        Assert.StartsWith("main", marked.Text);
        Assert.EndsWith("...", marked.Text);
        Assert.Equal(Width, TextWidth.Of(marked.Text));
    }

    [Fact]
    public void GivenARowThatIsNotTheCursor_WhenApplying_ThenItIsUnchanged()
    {
        var line = Line("main");

        Assert.Same(line, RowMenuMarker.Apply(line, Width, isCursor: false));
    }

    [Fact]
    public void GivenAPaneBelowTheMinimumWidth_WhenApplying_ThenNoMarkerIsShown()
    {
        var line = Line("main");

        Assert.Same(line, RowMenuMarker.Apply(line, RowMenuMarker.MinimumPaneWidth - 1, isCursor: true));
    }

    [Fact]
    public void GivenAWideCharacterAtTheCutPoint_WhenApplying_ThenItIsNotSplit()
    {
        var wide = new string('世', 20);

        var marked = RowMenuMarker.Apply(Line(wide), Width, isCursor: true);

        Assert.EndsWith("...", marked.Text);
        Assert.Equal(Width, TextWidth.Of(marked.Text));
    }

    [Theory]
    [InlineData(30, 27, true, true)]
    [InlineData(30, 29, true, true)]
    [InlineData(30, 26, true, false)]
    [InlineData(30, 30, true, false)]
    [InlineData(30, 28, false, false)]
    [InlineData(19, 17, true, false)]
    public void GivenAClick_WhenHitTesting_ThenOnlyTheMarkerColumnsOfTheSelectedRowHit(
        int paneWidth,
        int column,
        bool isSelectedRow,
        bool expected)
    {
        Assert.Equal(expected, RowMenuMarker.IsHit(paneWidth, column, isSelectedRow));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void GivenThePadding_WhenReserving_ThenPaddingAndReservedColumnsHoldTheMarker(int padding, int expected)
    {
        Assert.Equal(expected, RowMenuMarker.ReservedColumns(Width, padding));
        Assert.Equal(0, RowMenuMarker.ReservedColumns(RowMenuMarker.MinimumPaneWidth - 1, padding));
    }

    private static StyledLine Line(string text) =>
        new LineBuilder(Width, "~").Append(text, SegmentRole.Plain).Build();
}
