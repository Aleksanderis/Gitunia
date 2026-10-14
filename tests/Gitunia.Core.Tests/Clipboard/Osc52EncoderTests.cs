using System.Text;
using Gitunia.Core.Clipboard;
using Xunit;

namespace Gitunia.Core.Tests.Clipboard;

public class Osc52EncoderTests
{
    private static readonly Osc52Options Direct = new(Osc52Mode.Direct, 100_000);

    [Fact]
    public void GivenText_WhenEncodingDirect_ThenOscSequenceWithBase64Utf8()
    {
        var result = Osc52Encoder.Encode("héllo", Direct);

        Assert.Equal(Osc52Status.Ready, result.Status);
        Assert.Equal($"\u001b]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes("héllo"))}\u001b\\", result.Sequence);
    }

    [Fact]
    public void GivenText_WhenEncodingForTmux_ThenPassthroughWithDoubledEscapes()
    {
        var result = Osc52Encoder.Encode("abc", new Osc52Options(Osc52Mode.Tmux, 100_000));

        Assert.Equal("\u001bPtmux;\u001b\u001b]52;c;YWJj\u001b\u001b\\\u001b\\", result.Sequence);
    }

    [Fact]
    public void GivenLongText_WhenEncodingForScreen_ThenEveryChunkIsItsOwnDcsAndPayloadIsPreserved()
    {
        var text = new string('x', 300);

        var result = Osc52Encoder.Encode(text, new Osc52Options(Osc52Mode.Screen, 100_000));

        var chunks = result.Sequence.Split("\u001b\\", StringSplitOptions.RemoveEmptyEntries);
        Assert.True(chunks.Length > 1);
        Assert.All(chunks, chunk => Assert.StartsWith("\u001bP", chunk, StringComparison.Ordinal));
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 2 + 76));
        var joined = string.Concat(chunks.Select(chunk => chunk[2..]));
        Assert.Equal($"\u001b]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}\a", joined);
    }

    [Fact]
    public void GivenAnyTextLength_WhenEncodingForScreen_ThenOnlyTheLeadingEscapeAppearsInsideChunks()
    {
        for (var length = 1; length <= 200; length++)
        {
            var text = new string('x', length);

            var result = Osc52Encoder.Encode(text, new Osc52Options(Osc52Mode.Screen, 100_000));

            var chunks = result.Sequence.Split("\u001b\\", StringSplitOptions.RemoveEmptyEntries);
            var joined = string.Concat(chunks.Select(chunk => chunk[2..]));
            Assert.Equal($"\u001b]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}\a", joined);
            Assert.Equal(1, joined.Count(character => character == '\u001b'));
        }
    }

    [Fact]
    public void GivenEmptyText_WhenEncoding_ThenEmptyStatusAndNoSequence()
    {
        var result = Osc52Encoder.Encode(string.Empty, Direct);

        Assert.Equal(Osc52Status.Empty, result.Status);
        Assert.Equal(string.Empty, result.Sequence);
    }

    [Fact]
    public void GivenTextAtTheLimit_WhenEncoding_ThenReady()
    {
        var result = Osc52Encoder.Encode(new string('a', 30), new Osc52Options(Osc52Mode.Direct, 40));

        Assert.Equal(Osc52Status.Ready, result.Status);
    }

    [Fact]
    public void GivenTextOverTheLimit_WhenEncoding_ThenTooLargeWithMessage()
    {
        var result = Osc52Encoder.Encode(new string('a', 31), new Osc52Options(Osc52Mode.Direct, 40));

        Assert.Equal(Osc52Status.TooLarge, result.Status);
        Assert.Equal(string.Empty, result.Sequence);
        Assert.Contains("Too large", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMultibyteTextOverTheLimit_WhenEncoding_ThenLimitCountsUtf8Bytes()
    {
        var result = Osc52Encoder.Encode(new string('é', 16), new Osc52Options(Osc52Mode.Direct, 40));

        Assert.Equal(Osc52Status.TooLarge, result.Status);
    }

    [Fact]
    public void GivenUnsupportedTerminal_WhenEncoding_ThenUnsupportedWithReason()
    {
        var result = Osc52Encoder.Encode("abc", new Osc52Options(Osc52Mode.Unsupported, 100_000, "the terminal type 'dumb' has no clipboard support"));

        Assert.Equal(Osc52Status.Unsupported, result.Status);
        Assert.Contains("dumb", result.Message, StringComparison.Ordinal);
    }
}
