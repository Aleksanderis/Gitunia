using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Text;

[Trait("Os", "Independent")]
public class PrintableTextTests
{
    [Fact]
    public void GivenPlainText_WhenMadePrintable_ThenItIsReturnedAsIs()
    {
        Assert.Equal("src/a.txt", PrintableText.Of("src/a.txt"));
    }

    [Fact]
    public void GivenAValidSurrogatePair_WhenMadePrintable_ThenItIsKept()
    {
        Assert.Equal("a\U0001F600b", PrintableText.Of("a\U0001F600b"));
    }

    [Fact]
    public void GivenLoneSurrogates_WhenMadePrintable_ThenEachBecomesTheReplacementCharacter()
    {
        Assert.Equal("a\uFFFDb\uFFFD", PrintableText.Of("a\uD800b\uDC00"));
    }
}
