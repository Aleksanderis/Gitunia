using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Text;

[Trait("Os", "Independent")]
public class ShownColumnsTests
{
    private const int TabWidth = 4;

    [Fact]
    public void GivenPlainText_WhenConverting_ThenIndexAndColumnAreTheSame()
    {
        Assert.Equal(3, ShownColumns.OfIndex("abcdef", 3, TabWidth));
        Assert.Equal(3, ShownColumns.IndexAt("abcdef", 3, TabWidth));
    }

    [Fact]
    public void GivenTabs_WhenConverting_ThenTheyTakeColumnsUpToTheNextStop()
    {
        const string text = "\tab\tc";

        Assert.Equal(4, ShownColumns.OfIndex(text, 1, TabWidth));
        Assert.Equal(8, ShownColumns.OfIndex(text, 4, TabWidth));
        Assert.Equal(0, ShownColumns.IndexAt(text, 3, TabWidth));
        Assert.Equal(1, ShownColumns.IndexAt(text, 4, TabWidth));
        Assert.Equal(3, ShownColumns.IndexAt(text, 7, TabWidth));
        Assert.Equal(2, ShownColumns.WidthAt(text, 3, TabWidth));
    }

    [Fact]
    public void GivenWideCharacters_WhenConverting_ThenBothColumnsMapToTheCharacter()
    {
        const string text = "a中b";

        Assert.Equal(1, ShownColumns.OfIndex(text, 1, TabWidth));
        Assert.Equal(3, ShownColumns.OfIndex(text, 2, TabWidth));
        Assert.Equal(1, ShownColumns.IndexAt(text, 1, TabWidth));
        Assert.Equal(1, ShownColumns.IndexAt(text, 2, TabWidth));
        Assert.Equal(2, ShownColumns.IndexAt(text, 3, TabWidth));
        Assert.Equal(2, ShownColumns.WidthAt(text, 1, TabWidth));
    }

    [Fact]
    public void GivenAColumnPastTheEnd_WhenConverting_ThenTheLastCharacterIsReturned()
    {
        Assert.Equal(2, ShownColumns.IndexAt("abc", 99, TabWidth));
        Assert.Equal(0, ShownColumns.IndexAt(string.Empty, 5, TabWidth));
    }
}
