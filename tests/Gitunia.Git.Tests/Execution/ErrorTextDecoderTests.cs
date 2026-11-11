using System.Text;
using Gitunia.Git.Execution;
using Xunit;

namespace Gitunia.Git.Tests.Execution;

public class ErrorTextDecoderTests
{
    [Fact]
    public void GivenCrLfLineEnds_WhenDecoding_ThenTheCarriageReturnsAreStrippedButProgressRedrawsStay()
    {
        var bytes = Encoding.UTF8.GetBytes("hook failed\r\nsecond line\r\na 1%\ra 2%");

        Assert.Equal("hook failed\nsecond line\na 1%\ra 2%", ErrorTextDecoder.Decode(bytes, null));
    }

    [Fact]
    public void GivenValidUtf8_WhenDecodingWithFallback_ThenUtf8IsUsed()
    {
        var bytes = Encoding.UTF8.GetBytes("błąd");

        Assert.Equal("błąd", ErrorTextDecoder.Decode(bytes, Encoding.Latin1));
    }

    [Fact]
    public void GivenInvalidUtf8_WhenDecodingWithFallback_ThenTheFallbackEncodingIsUsed()
    {
        byte[] bytes = [0x66, 0xE9, 0x0D, 0x0A];

        Assert.Equal("fé\n", ErrorTextDecoder.Decode(bytes, Encoding.Latin1));
    }

    [Fact]
    public void GivenInvalidUtf8_WhenDecodingWithoutFallback_ThenInvalidBytesAreReplaced()
    {
        byte[] bytes = [0x66, 0xE9];

        Assert.Equal("f�", ErrorTextDecoder.Decode(bytes, null));
    }
}
