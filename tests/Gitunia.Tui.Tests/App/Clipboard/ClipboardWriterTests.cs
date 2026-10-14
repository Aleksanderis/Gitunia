using Gitunia.Core.Clipboard;
using Gitunia.Tui.App.Clipboard;
using Xunit;

namespace Gitunia.Tui.Tests.App.Clipboard;

public class ClipboardWriterTests
{
    [Fact]
    public void GivenSupportedTerminal_WhenCopying_ThenSequenceIsWrittenAndConfirmed()
    {
        var output = new StringWriter();
        var writer = new ClipboardWriter(output, new Osc52Options(Osc52Mode.Direct, 100_000));

        var outcome = writer.Run(new CopyToClipboardEffect("abc", "commit hash"));

        Assert.True(outcome.Succeeded);
        Assert.Equal("\u001b]52;c;YWJj\u001b\\", output.ToString());
        Assert.Contains("Copied commit hash", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenUnsupportedTerminal_WhenCopying_ThenNothingIsWrittenAndMessageExplains()
    {
        var output = new StringWriter();
        var writer = new ClipboardWriter(output, Osc52Environment.Detect(name => name == "TERM" ? "dumb" : null));

        var outcome = writer.Run(new CopyToClipboardEffect("abc", "commit hash"));

        Assert.False(outcome.Succeeded);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("dumb", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTooLargeText_WhenCopying_ThenNothingIsWritten()
    {
        var output = new StringWriter();
        var writer = new ClipboardWriter(output, new Osc52Options(Osc52Mode.Direct, 8));

        var outcome = writer.Run(new CopyToClipboardEffect("abcdefghij", "diff"));

        Assert.False(outcome.Succeeded);
        Assert.Equal(string.Empty, output.ToString());
    }
}
