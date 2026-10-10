using System.Text;
using Xunit;

namespace Gitunia.CustomRenderTui.Tests;

public class InputParserTests
{
    private static List<KeyEvent> Parse(params string[] reads)
    {
        var parser = new InputParser();
        var keys = new List<KeyEvent>();
        foreach (var read in reads)
        {
            parser.Feed(Encoding.UTF8.GetBytes(read), keys);
        }

        return keys;
    }

    [Fact]
    public void GivenPlainCharacters_WhenParsing_ThenEachBecomesTextKey()
    {
        var keys = Parse("jk");

        Assert.Equal([new KeyEvent(KeyKind.Text, 'j'), new KeyEvent(KeyKind.Text, 'k')], keys);
    }

    [Fact]
    public void GivenControlByte_WhenParsing_ThenItIsCtrlPlusLetter()
    {
        var keys = Parse("\u0004"); // Ctrl-D

        Assert.Equal([new KeyEvent(KeyKind.Text, 'd', Ctrl: true)], keys);
    }

    [Theory]
    [InlineData("\t", KeyKind.Tab)]
    [InlineData("\r", KeyKind.Enter)]
    [InlineData("\u007f", KeyKind.Backspace)]
    [InlineData("\e[A", KeyKind.Up)]
    [InlineData("\e[B", KeyKind.Down)]
    [InlineData("\e[C", KeyKind.Right)]
    [InlineData("\e[D", KeyKind.Left)]
    [InlineData("\eOA", KeyKind.Up)]
    [InlineData("\e[H", KeyKind.Home)]
    [InlineData("\e[F", KeyKind.End)]
    [InlineData("\e[5~", KeyKind.PageUp)]
    [InlineData("\e[6~", KeyKind.PageDown)]
    [InlineData("\e[<64;10;5M", KeyKind.WheelUp)]
    [InlineData("\e[<65;10;5M", KeyKind.WheelDown)]
    public void GivenSpecialKeySequence_WhenParsing_ThenKindMatches(string input, KeyKind expected)
    {
        var keys = Parse(input);

        Assert.Equal(expected, Assert.Single(keys).Kind);
    }

    [Fact]
    public void GivenLoneEscape_WhenParsing_ThenEscapeIsReportedImmediately()
    {
        var keys = Parse("\e");

        Assert.Equal(KeyKind.Escape, Assert.Single(keys).Kind);
    }

    [Fact]
    public void GivenArrowSplitAcrossReads_WhenParsing_ThenItCompletesOnTheSecondRead()
    {
        var parser = new InputParser();
        var keys = new List<KeyEvent>();

        parser.Feed("\e["u8, keys);
        Assert.Empty(keys);

        parser.Feed("A"u8, keys);
        Assert.Equal(KeyKind.Up, Assert.Single(keys).Kind);
    }

    [Fact]
    public void GivenUtf8CharacterSplitAcrossReads_WhenParsing_ThenItCompletesOnTheSecondRead()
    {
        var parser = new InputParser();
        var keys = new List<KeyEvent>();
        var bytes = Encoding.UTF8.GetBytes("é");

        parser.Feed(bytes.AsSpan(0, 1), keys);
        Assert.Empty(keys);

        parser.Feed(bytes.AsSpan(1), keys);
        Assert.Equal(new KeyEvent(KeyKind.Text, 'é'), Assert.Single(keys));
    }

    [Fact]
    public void GivenEndlessUnterminatedSequence_WhenParsing_ThenItIsDroppedAndLaterKeysStillWork()
    {
        var parser = new InputParser();
        var keys = new List<KeyEvent>();

        parser.Feed(Encoding.ASCII.GetBytes("\e[" + new string('1', 40)), keys);
        parser.Feed("j"u8, keys);

        Assert.Equal(new KeyEvent(KeyKind.Text, 'j'), Assert.Single(keys));
    }

    [Fact]
    public void GivenSequenceFollowedByText_WhenParsingInOneRead_ThenBothAreReported()
    {
        var keys = Parse("\e[Bj");

        Assert.Equal([KeyKind.Down, KeyKind.Text], keys.Select(k => k.Kind));
    }
}
