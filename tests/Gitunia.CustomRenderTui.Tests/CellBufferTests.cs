using System.Text;
using Xunit;

namespace Gitunia.CustomRenderTui.Tests;

public class CellBufferTests
{
    private static string Flush(CellBuffer buf) => Encoding.UTF8.GetString(buf.Flush());

    private static CellBuffer Blank(int w = 10, int h = 3)
    {
        var buf = new CellBuffer();
        buf.Resize(w, h);
        buf.Clear();
        return buf;
    }

    [Fact]
    public void GivenNewBuffer_WhenFlushingFirstTime_ThenClearsScreenAndWritesContent()
    {
        var buf = Blank();
        buf.Put(2, 1, "Hi", Style.None);

        var output = Flush(buf);

        Assert.StartsWith("\e[2J", output, StringComparison.Ordinal);
        Assert.Contains("Hi", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenFlushedBuffer_WhenNothingChanged_ThenFlushIsEmpty()
    {
        var buf = Blank();
        buf.Put(0, 0, "Hi", Style.None);
        Flush(buf);

        Assert.Empty(buf.Flush());
    }

    [Fact]
    public void GivenFlushedBuffer_WhenOneCellChanges_ThenOnlyThatCellIsEmitted()
    {
        var buf = Blank();
        buf.Put(0, 0, "Hi", Style.None);
        Flush(buf);

        buf.Put(4, 2, "X", Style.None);

        Assert.Equal("\e[3;5HX\e[0m", Flush(buf));
    }

    [Fact]
    public void GivenFlushedBuffer_WhenResized_ThenNextFlushRedrawsEverything()
    {
        var buf = Blank();
        buf.Put(0, 0, "Hi", Style.None);
        Flush(buf);

        buf.Resize(12, 4);
        buf.Clear();
        buf.Put(0, 0, "Hi", Style.None);
        var output = Flush(buf);

        Assert.StartsWith("\e[2J", output, StringComparison.Ordinal);
        Assert.Equal((12 * 4) - 2, output.Count(c => c == ' ')); // every cell except "Hi" is written, even though unchanged
    }

    [Fact]
    public void GivenStyledCell_WhenFlushing_ThenEmitsStyleSequenceAndResetsAfterwards()
    {
        var buf = Blank(4, 1);
        buf.Put(0, 0, "A", Style.Bold | Style.Reverse);

        var output = Flush(buf);

        Assert.Contains("\e[0;7;1mA", output, StringComparison.Ordinal);
        Assert.EndsWith("\e[0m", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenColouredCell_WhenFlushing_ThenEmits256ColourSequence()
    {
        var buf = Blank(4, 1);
        buf.Put(0, 0, "A", Style.None, 75);

        Assert.Contains("\e[0;38;5;75mA", Flush(buf), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTextWiderThanBuffer_WhenPutting_ThenItIsClippedWithoutThrowing()
    {
        var buf = Blank(4, 1);
        buf.Put(2, 0, "ABCDEF", Style.None);
        buf.Put(0, 5, "off screen", Style.None);

        var output = Flush(buf);

        Assert.Contains("AB", output, StringComparison.Ordinal);
        Assert.DoesNotContain("C", output, StringComparison.Ordinal);
        Assert.DoesNotContain("off", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenControlCharacters_WhenPutting_ThenTheyAreShownAsSpaces()
    {
        var buf = Blank(4, 1);
        buf.Put(0, 0, "a\tb", Style.None);

        var output = Flush(buf);

        Assert.DoesNotContain('\t', output);
        Assert.Contains("a b", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenBox_WhenDrawingWithTitle_ThenCornersAndTitleAreDrawn()
    {
        var buf = Blank(20, 4);
        buf.Box(0, 0, 20, 4, "Refs", Style.None);

        var output = Flush(buf);

        Assert.Contains("┌", output, StringComparison.Ordinal);
        Assert.Contains("┘", output, StringComparison.Ordinal);
        Assert.Contains(" Refs ", output, StringComparison.Ordinal);
    }
}
