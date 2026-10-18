using System.Text;
using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Text;

public class DisplayWidthTests
{
    [Theory]
    [InlineData("abc", 3)]
    [InlineData("中文", 4)]
    [InlineData("\U0001F600", 2)]
    [InlineData("é", 1)]
    [InlineData("\t", 1)]
    [InlineData("", 0)]
    public void GivenText_WhenMeasuring_ThenWideTakesTwoCombiningNoneAndControlsOne(string text, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(text));
    }

    [Theory]
    [InlineData(0x231A)] // Watch
    [InlineData(0x23E9)] // Fast-forward
    [InlineData(0x2614)] // Umbrella with rain
    [InlineData(0x2705)] // Check mark button
    [InlineData(0x26A1)] // High voltage
    [InlineData(0x2B50)] // Star
    [InlineData(0x1F680)] // Rocket
    [InlineData(0x1F7E0)] // Orange circle
    [InlineData(0x1FA70)] // Ballet shoes (Unicode 12)
    [InlineData(0x1FAC3)] // Pregnant man (Unicode 14)
    [InlineData(0x1FAE0)] // Melting face (Unicode 14)
    [InlineData(0x1FAF8)] // Rightwards pushing hand (Unicode 15)
    [InlineData(0x3000)] // Ideographic space
    [InlineData(0x30000)] // CJK extension G
    public void GivenWideCodePoint_WhenMeasuring_ThenTakesTwoColumns(int codePoint)
    {
        Assert.Equal(2, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Theory]
    [InlineData(0x10FF)] // Just before Hangul Jamo
    [InlineData(0x2764)] // Heavy heart (text presentation)
    [InlineData(0x00A9)] // Copyright sign
    [InlineData(0x1F1E6)] // Regional indicator
    [InlineData(0x3FFFE)] // Noncharacter after the last wide plane range
    [InlineData(0x1F8FF)] // Unassigned, outside every range
    public void GivenNarrowCodePoint_WhenMeasuring_ThenTakesOneColumn(int codePoint)
    {
        Assert.Equal(1, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Theory]
    [InlineData(0x115F, 2)]
    [InlineData(0x1160, 1)]
    [InlineData(0xA4C6, 2)]
    [InlineData(0xAC00, 2)]
    [InlineData(0xD7A3, 2)]
    [InlineData(0xFF60, 2)]
    [InlineData(0xFF61, 1)]
    [InlineData(0xFFE6, 2)]
    [InlineData(0xFFE7, 1)]
    public void GivenRangeEdge_WhenMeasuring_ThenBoundaryIsExact(int codePoint, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(new Rune(codePoint)));
    }
}
