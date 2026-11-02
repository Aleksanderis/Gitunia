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

    [Fact]
    public void GivenWideCodePoint_WhenMeasuring_ThenTakesTwoColumns()
    {
        var failures = new List<string>();
        int[] rows =
        [
            0x231A,
            0x23E9,
            0x2614,
            0x2705,
            0x26A1,
            0x2B50,
            0x1F680,
            0x1F7E0,
            0x1FA70,
            0x1FAC3,
            0x1FAE0,
            0x3000,
            0x30000,
        ];

        foreach (var codePoint in rows)
        {
            try
            {
                CheckTakesTwoColumns(codePoint);
            }
            catch (Exception exception)
            {
                failures.Add($"{codePoint}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckTakesTwoColumns(int codePoint)
    {
        Assert.Equal(2, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Fact]
    public void GivenNarrowCodePoint_WhenMeasuring_ThenTakesOneColumn()
    {
        var failures = new List<string>();
        int[] rows =
        [
            0x10FF,
            0x2764,
            0x00A9,
            0x1F1E6,
            0x3FFFE,
            0x1F8FF,
        ];

        foreach (var codePoint in rows)
        {
            try
            {
                CheckTakesOneColumn(codePoint);
            }
            catch (Exception exception)
            {
                failures.Add($"{codePoint}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckTakesOneColumn(int codePoint)
    {
        Assert.Equal(1, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Fact]
    public void GivenRangeEdge_WhenMeasuring_ThenBoundaryIsExact()
    {
        var failures = new List<string>();
        (int codePoint, int expected)[] rows =
        [
            (0x115F, 2),
            (0x1160, 0),
            (0xA4C6, 2),
            (0xAC00, 2),
            (0xD7A3, 2),
            (0xFF60, 2),
            (0xFF61, 1),
            (0xFFE6, 2),
            (0xFFE7, 1),
            (0x1F320, 2),
            (0x1F321, 1),
            (0x1F32C, 1),
            (0x1F32D, 2),
            (0x1FAC6, 2),
            (0x1FAC7, 1),
            (0x1FAF8, 2),
            (0x1FAF9, 1),
        ];

        foreach (var (codePoint, expected) in rows)
        {
            try
            {
                CheckBoundaryIsExact(codePoint, expected);
            }
            catch (Exception exception)
            {
                failures.Add($"{codePoint}, {expected}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckBoundaryIsExact(int codePoint, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Fact]
    public void GivenZeroWidthJoinerSequence_WhenMeasuring_ThenItIsOneWideCell()
    {
        var failures = new List<string>();
        (string text, int expected)[] rows =
        [
            ("\U0001F468‍\U0001F469‍\U0001F467", 2),
            ("\U0001F469‍❤️‍\U0001F468", 2),
            ("❤️‍\U0001F525", 2),
            ("a‍b", 2),
            ("‍\U0001F468", 2),
            ("\U0001F468‍", 2),
            ("\U0001F468‍a", 3),
            ("\U0001F468‍‍\U0001F469", 4),
            ("\U0001F468‍\U0001F469\U0001F467", 4),
        ];

        foreach (var (text, expected) in rows)
        {
            try
            {
                CheckItIsOneWideCell(text, expected);
            }
            catch (Exception exception)
            {
                failures.Add($"{text}, {expected}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckItIsOneWideCell(string text, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(text));
    }
}
