using Gitunia.Core.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

[Trait("Os", "Independent")]
public class KeyFormatterTests
{
    private const string ThinSpace = " ";

    [Fact]
    public void GivenLeaderAndEquals_WhenDisplaying_ThenAThinSpaceKeepsTheLigatureApart()
    {
        var text = KeyFormatter.Display(KeySequenceParser.Parse("<space>="));

        Assert.Equal($"<space>{ThinSpace}=", text);
    }

    [Fact]
    public void GivenLeaderAndEquals_WhenFormatting_ThenTheConfigTextStaysPlain()
    {
        Assert.Equal("<space>=", KeyFormatter.Format(KeySequenceParser.Parse("<space>=")));
    }

    [Theory]
    [InlineData("<space>s", "<space>s")]
    [InlineData("<space>-", "<space> -")]
    [InlineData("gg", "gg")]
    public void GivenOtherSequences_WhenBreakingLigatures_ThenOnlyAngleBracketPairsChange(string text, string expected)
    {
        Assert.Equal(expected, KeyFormatter.BreakLigatures(text));
    }
}
