using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Text;

[Trait("Os", "Independent")]
public class TextMotionsTests
{
    [Fact]
    public void GivenAColumn_WhenMovingRight_ThenItStopsOnTheLastCharacter()
    {
        Assert.Equal(1, TextMotions.Right("abc", 0));
        Assert.Equal(2, TextMotions.Right("abc", 1));
        Assert.Equal(2, TextMotions.Right("abc", 2));
        Assert.Equal(2, TextMotions.Right("abc", 0, 9));
    }

    [Fact]
    public void GivenAColumn_WhenMovingLeft_ThenItStopsOnTheFirstCharacter()
    {
        Assert.Equal(1, TextMotions.Left("abc", 2));
        Assert.Equal(0, TextMotions.Left("abc", 0));
        Assert.Equal(0, TextMotions.Left("abc", 2, 5));
    }

    [Fact]
    public void GivenAnEmptyLine_WhenMoving_ThenTheColumnStaysZero()
    {
        Assert.Equal(0, TextMotions.Right(string.Empty, 0));
        Assert.Equal(0, TextMotions.Left(string.Empty, 3));
        Assert.Equal(0, TextMotions.WordForward(string.Empty, 0));
        Assert.Equal(0, TextMotions.WordBack(string.Empty, 0));
        Assert.Equal(0, TextMotions.LineEnd(string.Empty));
    }

    [Fact]
    public void GivenASurrogatePair_WhenMoving_ThenItStepsOverTheWholeCharacter()
    {
        const string text = "a\U0001F600b";

        Assert.Equal(1, TextMotions.Right(text, 0));
        Assert.Equal(3, TextMotions.Right(text, 1));
        Assert.Equal(1, TextMotions.Left(text, 3));
        Assert.Equal(3, TextMotions.LineEnd(text));
    }

    [Fact]
    public void GivenCode_WhenMovingByWord_ThenWordsAndPunctuationAreSeparateStops()
    {
        const string text = "foo.bar(baz)  qux";

        Assert.Equal(3, TextMotions.WordForward(text, 0));
        Assert.Equal(4, TextMotions.WordForward(text, 3));
        Assert.Equal(7, TextMotions.WordForward(text, 4));
        Assert.Equal(8, TextMotions.WordForward(text, 7));
        Assert.Equal(11, TextMotions.WordForward(text, 8));
        Assert.Equal(14, TextMotions.WordForward(text, 11));
        Assert.Equal(16, TextMotions.WordForward(text, 14, 9));
    }

    [Fact]
    public void GivenCode_WhenMovingBackByWord_ThenItReachesTheStartOfTheWord()
    {
        const string text = "foo.bar(baz)  qux";

        Assert.Equal(14, TextMotions.WordBack(text, 16));
        Assert.Equal(11, TextMotions.WordBack(text, 14));
        Assert.Equal(8, TextMotions.WordBack(text, 11));
        Assert.Equal(4, TextMotions.WordBack(text, 7));
        Assert.Equal(0, TextMotions.WordBack(text, 2));
        Assert.Equal(0, TextMotions.WordBack(text, 0));
    }

    [Fact]
    public void GivenAWord_WhenAskingForTheWordAt_ThenTheWholeRunIsReturned()
    {
        const string text = "let value_1 = 5;";

        Assert.Equal(new ColumnSpan(4, 10), TextMotions.WordAt(text, 7));
        Assert.Equal(new ColumnSpan(4, 10), TextMotions.WordAt(text, 4));
        Assert.Equal(new ColumnSpan(3, 3), TextMotions.WordAt(text, 3));
        Assert.Equal(new ColumnSpan(12, 12), TextMotions.WordAt(text, 12));
        Assert.Equal(new ColumnSpan(15, 15), TextMotions.WordAt(text, 40));
        Assert.Equal(new ColumnSpan(0, 0), TextMotions.WordAt(string.Empty, 5));
    }
}
