using System.Globalization;
using System.Text;
using Gitunia.Core.Text;
using Terminal.Gui.Text;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

public class TerminalGuiWidthComparisonTests(ITestOutputHelper output)
{
    private const int LastCodePoint = 0x10FFFF;
    private const int FirstSurrogate = 0xD800;
    private const int LastSurrogate = 0xDFFF;
    private const int SoftHyphen = 0xAD;
    private const int LineSeparator = 0x2028;
    private const int ParagraphSeparator = 0x2029;
    private const int EthiopicHa = 0x1200;
    private const int FirstDelete = 0x7F;
    private const int FirstPrintableLatin1 = 0xA0;
    private const int FirstPrintableAscii = 0x20;
    private const int ListedMismatches = 40;

    [Theory]
    [InlineData("😀", 2)]
    [InlineData("修", 2)]
    [InlineData("é", 1)]
    [InlineData("a", 1)]
    [InlineData("👍🏽", 2)]
    [InlineData("🇩🇪", 2)]
    [InlineData("❤️", 1)]
    [InlineData("é", 1)] // Decomposed é
    [InlineData("가", 2)]
    public void GivenCommonText_WhenMeasuredByBoth_ThenTerminalGuiAgreesWithDisplayWidth(string text, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(text));
        Assert.Equal(expected, text.GetColumns());
    }

    [Fact]
    public void GivenEveryCodePoint_WhenComparingWidths_ThenOnlyTheDocumentedOnesDiffer()
    {
        var unexpected = new List<string>();

        for (var value = 0; value <= LastCodePoint; value++)
        {
            var skipped = value is >= FirstSurrogate and <= LastSurrogate
                || value < FirstPrintableAscii
                || value is >= FirstDelete and < FirstPrintableLatin1;
            if (skipped)
            {
                continue;
            }

            var rune = new Rune(value);
            var ours = DisplayWidth.Of(rune);
            var theirs = rune.GetColumns();
            if (ours != theirs && !IsDocumentedDifference(rune))
            {
                unexpected.Add($"U+{value:X4} gitunia={ours} terminal.gui={theirs}");
            }
        }

        output.WriteLine(string.Join(Environment.NewLine, unexpected.Take(ListedMismatches)));
        Assert.Empty(unexpected);
    }

    [Fact]
    public void GivenZeroWidthJoinerSequences_WhenMeasuredByBoth_ThenTerminalGuiAgrees()
    {
        string[] sequences =
        [
            "\U0001F468‍\U0001F469‍\U0001F467", // Family
            "\U0001F469‍❤️‍\U0001F468", // Couple with heart
            "\U0001F3C3‍♀️", // Woman running
            "\U0001F468\U0001F3FD‍\U0001F4BB", // Man technologist, skin tone
            "\U0001F3F3️‍\U0001F308", // Rainbow flag
            "❤️‍\U0001F525", // Heart on fire
            "x\U0001F468‍\U0001F469y",
            "a‍b",
            "\U0001F468‍a",
            "\U0001F468‍日",
            "\U0001F468‍ ",
            "\U0001F468‍1",
            "\U0001F468‍‍\U0001F469",
            "❤‍\U0001F525",
            "\U0001F468‍\U0001F469\U0001F467",
        "‍\U0001F468",
        "\U0001F468‍",
        ];

        var disagreeing = sequences.Where(text => text.GetColumns() != DisplayWidth.Of(text)).ToList();

        Assert.Empty(disagreeing);
    }

    // Soft hyphen (Terminal.Gui 1, we 0), the line and paragraph separators and U+1200 (we 1, Terminal.Gui 0, an
    // off-by-one at the end of its Hangul Jamo range) and unassigned code points: none occurs in git text that
    // matters, and the terminal's own choice is unknown.
    private static bool IsDocumentedDifference(Rune rune)
    {
        return rune.Value is SoftHyphen or LineSeparator or ParagraphSeparator or EthiopicHa
            || Rune.GetUnicodeCategory(rune) == UnicodeCategory.OtherNotAssigned;
    }
}
