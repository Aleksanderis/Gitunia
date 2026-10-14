using Xunit;

namespace Gitunia.Git.Tests;

public class ObjectIdTests
{
    private const string Sha1 = "0123456789abcdef0123456789abcdef01234567";
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(Sha1)]
    [InlineData(Sha256)]
    public void GivenHex_WhenParsingAndPrinting_ThenRoundTrips(string hex)
    {
        Assert.True(ObjectId.TryParse(hex, out var id));
        Assert.Equal(hex, id.ToString());
        Assert.True(id.IsValid);
    }

    [Fact]
    public void GivenUppercaseHex_WhenParsing_ThenEqualsLowercase()
    {
        Assert.True(ObjectId.TryParse(Sha1, out var lower));
        Assert.True(ObjectId.TryParse(Sha1.ToUpperInvariant(), out var upper));

        Assert.Equal(lower, upper);
        Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef0123456z")]
    [InlineData("0123456789abcdef0123456789abcdef012345678")]
    public void GivenInvalidHex_WhenParsing_ThenFalse(string hex)
    {
        Assert.False(ObjectId.TryParse(hex, out var id));
        Assert.False(id.IsValid);
    }

    [Fact]
    public void GivenDifferentIds_WhenComparing_ThenNotEqual()
    {
        Assert.True(ObjectId.TryParse(Sha1, out var first));
        Assert.True(ObjectId.TryParse($"{Sha1[..^1]}8", out var second));

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }
}
