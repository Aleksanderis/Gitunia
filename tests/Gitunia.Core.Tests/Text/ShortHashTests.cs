using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Text;

[Trait("Os", "Independent")]
public sealed class ShortHashTests
{
    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef01234567", "0123456")]
    [InlineData("0123456", "0123456")]
    [InlineData("012", "012")]
    [InlineData("", "")]
    public void GivenAHash_WhenShortening_ThenAtMostSevenCharactersRemain(string hash, string expected)
    {
        Assert.Equal(expected, ShortHash.Of(hash));
    }

    [Fact]
    public void GivenNoHash_WhenShortening_ThenTheResultIsEmpty()
    {
        Assert.Equal(string.Empty, ShortHash.Of(null));
    }
}
