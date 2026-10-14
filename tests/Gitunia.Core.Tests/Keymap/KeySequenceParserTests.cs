using Gitunia.Core.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

public class KeySequenceParserTests
{
    [Fact]
    public void GivenPlainCharacters_WhenParsing_ThenEachBecomesAKey()
    {
        var sequence = KeySequenceParser.Parse("gg");

        Assert.Equal([new KeyEvent("g"), new KeyEvent("g")], sequence.Keys);
    }

    [Fact]
    public void GivenModifiedKeyThenCharacter_WhenParsing_ThenItIsAChord()
    {
        var sequence = KeySequenceParser.Parse("Ctrl-w h");

        Assert.Equal([new KeyEvent("w", KeyModifiers.Ctrl), new KeyEvent("h")], sequence.Keys);
    }

    [Fact]
    public void GivenLeaderNotation_WhenParsing_ThenSpaceIsTheFirstKey()
    {
        var sequence = KeySequenceParser.Parse("<space><Tab>");

        Assert.Equal([new KeyEvent(" "), new KeyEvent(KeyNames.Tab)], sequence.Keys);
    }

    [Fact]
    public void GivenNamedAndShiftedKeys_WhenParsing_ThenCanonicalNamesAreUsed()
    {
        Assert.Equal([new KeyEvent(KeyNames.Tab, KeyModifiers.Shift)], KeySequenceParser.Parse("Shift-Tab").Keys);
        Assert.Equal([new KeyEvent(KeyNames.Escape)], KeySequenceParser.Parse("Esc").Keys);
        Assert.Equal([new KeyEvent("1", KeyModifiers.Alt)], KeySequenceParser.Parse("Alt-1").Keys);
    }

    [Fact]
    public void GivenLiteralLessThan_WhenParsing_ThenLtNotationIsUsed()
    {
        Assert.Equal([new KeyEvent("<")], KeySequenceParser.Parse("<lt>").Keys);
    }

    [Theory]
    [InlineData("<space>o")]
    [InlineData("Ctrl-w c")]
    [InlineData("]c")]
    [InlineData("Alt-d")]
    public void GivenASequence_WhenFormattedAndParsedAgain_ThenItIsEqual(string text)
    {
        var sequence = KeySequenceParser.Parse(text);

        Assert.Equal(sequence, KeySequenceParser.Parse(sequence.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<unknown>")]
    [InlineData("<space")]
    public void GivenMalformedText_WhenParsing_ThenItFails(string text)
    {
        Assert.False(KeySequenceParser.TryParse(text, out _));
    }

    [Fact]
    public void GivenShiftedLetterAndCtrlUppercase_WhenNormalized_ThenTheyMatchTheDocumentedForms()
    {
        Assert.Equal(new KeyEvent("G"), new KeyEvent("G", KeyModifiers.Shift).Normalize());
        Assert.Equal(new KeyEvent("w", KeyModifiers.Ctrl), new KeyEvent("W", KeyModifiers.Ctrl).Normalize());
    }
}
