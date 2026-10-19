using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

public class TextWidthTests
{
    [Theory]
    [InlineData("abc", 3)]
    [InlineData("修正", 4)]
    [InlineData("é", 1)]
    [InlineData("", 0)]
    public void GivenText_WhenMeasuring_ThenWideTakeTwoColumnsAndCombiningNone(string text, int expected)
    {
        Assert.Equal(expected, TextWidth.Of(text));
    }

    [Fact]
    public void GivenTextThatFits_WhenFitting_ThenItIsUnchanged()
    {
        Assert.Equal("hello", TextWidth.Fit("hello", 5, "…"));
    }

    [Fact]
    public void GivenTextThatDoesNotFit_WhenFitting_ThenEllipsisFillsTheLastColumn()
    {
        Assert.Equal("hell…", TextWidth.Fit("hello world", 5, "…"));
    }

    [Fact]
    public void GivenWideCharacterAtTheEdge_WhenFitting_ThenItIsDroppedNotSplit()
    {
        var fitted = TextWidth.Fit("ab修正", 4, "…");

        Assert.Equal("ab…", fitted);
        Assert.True(TextWidth.Of(fitted) <= 4);
    }

    [Fact]
    public void GivenJoinedEmoji_WhenFitting_ThenItCountsTwoColumnsAndIsKeptWhole()
    {
        const string family = "\U0001F468‍\U0001F469‍\U0001F467";

        Assert.Equal($"ab{family}", TextWidth.Fit($"ab{family}", 4, "…"));
        Assert.Equal($"a{family}…", TextWidth.Fit($"a{family}cdef", 4, "…"));
    }

    [Fact]
    public void GivenJoinedEmojiThatDoesNotFit_WhenFitting_ThenItIsDroppedNotCut()
    {
        const string family = "\U0001F468‍\U0001F469‍\U0001F467";

        Assert.Equal("abc…", TextWidth.Fit($"abc{family}", 4, "…"));
    }

    [Fact]
    public void GivenNoRoom_WhenFitting_ThenResultIsEmpty()
    {
        Assert.Equal(string.Empty, TextWidth.Fit("hello", 0, "…"));
    }
}
