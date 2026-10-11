using System.Text;
using Xunit;

namespace Gitunia.CustomRenderTui.Tests;

public class CellBufferTests
{
    private static string Flush(CellBuffer buffer) => Encoding.UTF8.GetString(buffer.Flush());

    private static CellBuffer Blank(int width = 10, int height = 3)
    {
        var buffer = new CellBuffer();
        buffer.Resize(width, height);
        buffer.Clear();
        return buffer;
    }

    [Fact]
    public void GivenNewBuffer_WhenFlushingFirstTime_ThenClearsScreenAndWritesContent()
    {
        var buffer = Blank();
        buffer.Put(2, 1, "Hi", Style.None);

        var output = Flush(buffer);

        Assert.StartsWith("\e[2J", output, StringComparison.Ordinal);
        Assert.Contains("Hi", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenFlushedBuffer_WhenNothingChanged_ThenFlushIsEmpty()
    {
        var buffer = Blank();
        buffer.Put(0, 0, "Hi", Style.None);
        Flush(buffer);

        Assert.Empty(buffer.Flush());
    }

    [Fact]
    public void GivenFlushedBuffer_WhenOneCellChanges_ThenOnlyThatCellIsEmitted()
    {
        var buffer = Blank();
        buffer.Put(0, 0, "Hi", Style.None);
        Flush(buffer);

        buffer.Put(4, 2, "X", Style.None);

        Assert.Equal("\e[3;5HX\e[0m", Flush(buffer));
    }

    [Fact]
    public void GivenFlushedBuffer_WhenResized_ThenNextFlushRedrawsEverything()
    {
        var buffer = Blank();
        buffer.Put(0, 0, "Hi", Style.None);
        Flush(buffer);

        buffer.Resize(12, 4);
        buffer.Clear();
        buffer.Put(0, 0, "Hi", Style.None);
        var output = Flush(buffer);

        Assert.StartsWith("\e[2J", output, StringComparison.Ordinal);
        Assert.Equal((12 * 4) - 2, output.Count(character => character == ' ')); // every cell except "Hi" is written, even though unchanged
    }

    [Fact]
    public void GivenStyledCell_WhenFlushing_ThenEmitsStyleSequenceAndResetsAfterwards()
    {
        var buffer = Blank(4, 1);
        buffer.Put(0, 0, "A", Style.Bold | Style.Reverse);

        var output = Flush(buffer);

        Assert.Contains("\e[0;7;1mA", output, StringComparison.Ordinal);
        Assert.EndsWith("\e[0m", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenColouredCell_WhenFlushing_ThenEmits256ColourSequence()
    {
        var buffer = Blank(4, 1);
        buffer.Put(0, 0, "A", Style.None, 75);

        Assert.Contains("\e[0;38;5;75mA", Flush(buffer), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTextWiderThanBuffer_WhenPutting_ThenItIsClippedWithoutThrowing()
    {
        var buffer = Blank(4, 1);
        buffer.Put(2, 0, "ABCDEF", Style.None);
        buffer.Put(0, 5, "off screen", Style.None);

        var output = Flush(buffer);

        Assert.Contains("AB", output, StringComparison.Ordinal);
        Assert.DoesNotContain("C", output, StringComparison.Ordinal);
        Assert.DoesNotContain("off", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenControlCharacters_WhenPutting_ThenTheyAreShownAsSpaces()
    {
        var buffer = Blank(4, 1);
        buffer.Put(0, 0, "a\tb", Style.None);

        var output = Flush(buffer);

        Assert.DoesNotContain('\t', output);
        Assert.Contains("a b", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenBox_WhenDrawingWithTitle_ThenCornersAndTitleAreDrawn()
    {
        var buffer = Blank(20, 4);
        buffer.Box(0, 0, 20, 4, "Refs", Style.None);

        var output = Flush(buffer);

        Assert.Contains("┌", output, StringComparison.Ordinal);
        Assert.Contains("┘", output, StringComparison.Ordinal);
        Assert.Contains(" Refs ", output, StringComparison.Ordinal);
    }
}
