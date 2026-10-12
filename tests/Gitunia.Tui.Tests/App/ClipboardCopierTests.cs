using Gitunia.Core.Clipboard;
using Gitunia.Core.Status;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class ClipboardCopierTests
{
    [Fact]
    public void GivenSupportedTerminal_WhenCopying_ThenSequenceIsWrittenAndInfoPosted()
    {
        var output = new StringWriter();
        var statuses = new StatusChannel();
        var copier = new ClipboardCopier(new ClipboardWriter(output, new Osc52Options(Osc52Mode.Direct, 100_000)), statuses, () => 500);

        copier.Copy("abc", "commit hash");

        Assert.Equal("\u001b]52;c;YWJj\u001b\\", output.ToString());
        Assert.Equal(StatusSeverity.Info, statuses.Log.Latest?.Severity);
        Assert.Contains("Copied commit hash", statuses.Log.Latest?.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenUnsupportedTerminal_WhenCopying_ThenWarningExplainsAndNothingIsWritten()
    {
        var output = new StringWriter();
        var statuses = new StatusChannel();
        var writer = new ClipboardWriter(output, Osc52Environment.Detect(name => name == "TERM" ? "dumb" : null));

        new ClipboardCopier(writer, statuses, () => 500).Copy("abc", "commit hash");

        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(StatusSeverity.Warning, statuses.Log.Latest?.Severity);
    }

    [Fact]
    public void GivenNoWriter_WhenCopying_ThenWarningSaysConsoleIsNotInteractive()
    {
        var statuses = new StatusChannel();

        new ClipboardCopier(null, statuses, () => 500).Copy("abc", "commit hash");

        Assert.Equal(ClipboardCopier.NotInteractiveMessage, statuses.Log.Latest?.Text);
    }

    [Fact]
    public void GivenNarrowTerminal_WhenCopying_ThenMessageFitsTheWidth()
    {
        var statuses = new StatusChannel();
        var copier = new ClipboardCopier(new ClipboardWriter(new StringWriter(), new Osc52Options(Osc52Mode.Direct, 100_000)), statuses, () => 20);

        copier.Copy("abc", "commit hash");

        Assert.Equal(20, statuses.Log.Latest?.Text.Length);
        Assert.EndsWith("…", statuses.Log.Latest?.Text, StringComparison.Ordinal);
    }
}
