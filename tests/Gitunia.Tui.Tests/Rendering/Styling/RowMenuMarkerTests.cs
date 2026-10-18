using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

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

    private static StyledLine Line(string text) =>
        new LineBuilder(Width, "~").Append(text, SegmentRole.Plain).Build();
}
