using System.Text;
using Gitunia.Git.Execution;
using Xunit;

namespace Gitunia.Git.Tests.Execution;

public class GitPathTextTests
{
    [Fact]
    public void GivenValidUtf8_WhenDecodingAndEncoding_ThenTextAndBytesAreUnchanged()
    {
        var bytes = Encoding.UTF8.GetBytes("dir/zażółć 😀.txt");

        var text = GitPathText.Decode(bytes);

        Assert.Equal("dir/zażółć 😀.txt", text);
        Assert.Equal(bytes, GitPathText.Encode(text));
        Assert.False(GitPathText.HasEscapedBytes(text));
    }

    [Theory]
    [InlineData(new byte[] { 0x61, 0xFF, 0x2E })]
    [InlineData(new byte[] { 0xC3 })]
    [InlineData(new byte[] { 0xE2, 0x82 })]
    [InlineData(new byte[] { 0xF0, 0x9F, 0x98, 0x41 })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0xC3, 0xA9, 0x80 })]
    public void GivenInvalidUtf8_WhenDecodingAndEncoding_ThenBytesRoundTripExactly(byte[] bytes)
    {
        var text = GitPathText.Decode(bytes);

        Assert.True(GitPathText.HasEscapedBytes(text));
        Assert.Equal(bytes, GitPathText.Encode(text));
    }

    [Fact]
    public void GivenPaths_WhenEncodingPathspecFile_ThenEachPathEndsWithNul()
    {
        var bytes = GitPathText.EncodePathspecFile(["a b", GitPathText.Decode([0x78, 0xFF])]);

        Assert.Equal(new byte[] { 0x61, 0x20, 0x62, 0, 0x78, 0xFF, 0 }, bytes);
    }

    [Theory]
    [InlineData('\uDC00')]
    [InlineData('\uDD00')]
    [InlineData('\uDC7F')]
    public void GivenSurrogateThatDecodeNeverProduces_WhenEncoding_ThenRefused(char surrogate)
    {
        Assert.Throws<ArgumentException>(() => GitPathText.Encode($"a{surrogate}b"));
    }
}
