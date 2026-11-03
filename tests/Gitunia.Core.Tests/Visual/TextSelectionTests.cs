using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Xunit;

namespace Gitunia.Core.Tests.Visual;

[Trait("Os", "Independent")]
public class TextSelectionTests
{
    private static readonly string[] Sample = ["first line", "", "second", "third one"];

    [Fact]
    public void GivenOneLine_WhenSelected_ThenExactlyThoseCharactersAreCopied()
    {
        var selection = new TextSelection(new TextPosition(0, 2), new TextPosition(0, 6));

        Assert.Equal("rst l", selection.TextIn(new Lines(Sample)));
    }

    [Fact]
    public void GivenTheCursorBeforeTheAnchor_WhenSelected_ThenTheSameTextIsCopied()
    {
        var forward = new TextSelection(new TextPosition(0, 2), new TextPosition(2, 3));
        var backward = new TextSelection(new TextPosition(2, 3), new TextPosition(0, 2));

        Assert.Equal(forward.TextIn(new Lines(Sample)), backward.TextIn(new Lines(Sample)));
        Assert.Equal("rst line\n\nseco", forward.TextIn(new Lines(Sample)));
    }

    [Fact]
    public void GivenSeveralLines_WhenSelected_ThenTheMiddleOnesAreWholeAndNoTrailingLineFeedIsAdded()
    {
        var selection = new TextSelection(new TextPosition(0, 6), new TextPosition(3, 8));

        Assert.Equal("line\n\nsecond\nthird one", selection.TextIn(new Lines(Sample)));
    }

    [Fact]
    public void GivenSkippedLines_WhenSelected_ThenTheyAreLeftOut()
    {
        var selection = new TextSelection(new TextPosition(0, 0), new TextPosition(3, 8));

        Assert.Equal("first line\nsecond\nthird one", selection.TextIn(new Lines(Sample, skipped: 1)));
    }

    [Fact]
    public void GivenColumnsPastTheEnd_WhenSelected_ThenTheyAreClampedToTheLine()
    {
        var selection = new TextSelection(new TextPosition(2, 4), new TextPosition(2, 99));

        Assert.Equal("nd", selection.TextIn(new Lines(Sample)));
    }

    [Fact]
    public void GivenALineWithASurrogatePairAtTheEnd_WhenSelected_ThenThePairIsCopiedWhole()
    {
        var selection = new TextSelection(new TextPosition(0, 0), new TextPosition(0, 1));

        Assert.Equal("a\U0001F600", selection.TextIn(new Lines(["a\U0001F600b"])));
    }

    [Fact]
    public void GivenASelection_WhenAskingForALine_ThenOnlyCoveredLinesHaveASpan()
    {
        var selection = new TextSelection(new TextPosition(0, 6), new TextPosition(2, 1));

        Assert.Equal(new ColumnSpan(6, 9), selection.SpanOn(0, "first line"));
        Assert.Null(selection.SpanOn(1, string.Empty));
        Assert.Equal(new ColumnSpan(0, 1), selection.SpanOn(2, "second"));
        Assert.Null(selection.SpanOn(3, "third one"));
    }

    private sealed class Lines(string[] lines, int skipped = -1) : ITextLines
    {
        public int Count => lines.Length;

        public string TextOf(int line) => lines[line];

        public bool IsSkipped(int line) => line == skipped;
    }
}
