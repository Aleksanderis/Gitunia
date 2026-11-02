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
    [InlineData(0x231A)]
    [InlineData(0x23E9)]
    [InlineData(0x2614)]
    [InlineData(0x2705)]
    [InlineData(0x26A1)]
    [InlineData(0x2B50)]
    [InlineData(0x1F680)]
    [InlineData(0x1F7E0)]
    [InlineData(0x1FA70)]
    [InlineData(0x1FAC3)]
    [InlineData(0x1FAE0)]
    [InlineData(0x3000)]
    [InlineData(0x30000)]
    public void GivenWideCodePoint_WhenMeasuring_ThenTakesTwoColumns(int codePoint)
    {
        Assert.Equal(2, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Theory]
    [InlineData(0x10FF)]
    [InlineData(0x2764)]
    [InlineData(0x00A9)]
    [InlineData(0x1F1E6)]
    [InlineData(0x3FFFE)]
    [InlineData(0x1F8FF)]
    public void GivenNarrowCodePoint_WhenMeasuring_ThenTakesOneColumn(int codePoint)
    {
        Assert.Equal(1, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Theory]
    [InlineData(0x115F, 2)]
    [InlineData(0x1160, 0)]
    [InlineData(0xA4C6, 2)]
    [InlineData(0xAC00, 2)]
    [InlineData(0xD7A3, 2)]
    [InlineData(0xFF60, 2)]
    [InlineData(0xFF61, 1)]
    [InlineData(0xFFE6, 2)]
    [InlineData(0xFFE7, 1)]
    [InlineData(0x1F320, 2)]
    [InlineData(0x1F321, 1)]
    [InlineData(0x1F32C, 1)]
    [InlineData(0x1F32D, 2)]
    [InlineData(0x1FAC6, 2)]
    [InlineData(0x1FAC7, 1)]
    [InlineData(0x1FAF8, 2)]
    [InlineData(0x1FAF9, 1)]
    public void GivenRangeEdge_WhenMeasuring_ThenBoundaryIsExact(int codePoint, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Theory]
    [InlineData("\U0001F468‍\U0001F469‍\U0001F467", 2)]
    [InlineData("\U0001F469‍❤️‍\U0001F468", 2)]
    [InlineData("❤️‍\U0001F525", 2)]
    [InlineData("a‍b", 2)]
    [InlineData("‍\U0001F468", 2)]
    [InlineData("\U0001F468‍", 2)]
    [InlineData("\U0001F468‍a", 3)]
    [InlineData("\U0001F468‍‍\U0001F469", 4)]
    [InlineData("\U0001F468‍\U0001F469\U0001F467", 4)]
    public void GivenZeroWidthJoinerSequence_WhenMeasuring_ThenItIsOneWideCell(string text, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(text));
    }
}
