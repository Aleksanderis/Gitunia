using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Status;
using Gitunia.Core;
using Gitunia.Tui.App.Clipboard;
using Xunit;

namespace Gitunia.Tui.Tests.App.Clipboard;

public class ClipboardCopierTests
{
    [Fact]
    public void GivenSupportedTerminal_WhenCopying_ThenSequenceIsWrittenAndInfoPosted()
    {
        var output = new StringWriter();
        var statuses = new StatusReporter(new Dispatcher(AppState.Empty));
        var copier = new ClipboardCopier(new ClipboardWriter(output, new Osc52Options(Osc52Mode.Direct, 100_000)), statuses);

        Assert.True(copier.Copy("abc", "commit hash"));

        Assert.Equal("\u001b]52;c;YWJj\u001b\\", output.ToString());
        Assert.Equal(StatusSeverity.Info, statuses.Log.Latest?.Severity);
        Assert.Equal("Copied commit hash", statuses.Log.Latest?.Text);
    }

    [Fact]
    public void GivenUnsupportedTerminal_WhenCopying_ThenWarningExplainsAndNothingIsWritten()
    {
        var output = new StringWriter();
        var statuses = new StatusReporter(new Dispatcher(AppState.Empty));
        var writer = new ClipboardWriter(output, Osc52Environment.Detect(name => name == "TERM" ? "dumb" : null));

        Assert.False(new ClipboardCopier(writer, statuses).Copy("abc", "commit hash"));

        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(StatusSeverity.Warning, statuses.Log.Latest?.Severity);
    }

    [Fact]
    public void GivenNoWriter_WhenCopying_ThenWarningSaysConsoleIsNotInteractive()
    {
        var statuses = new StatusReporter(new Dispatcher(AppState.Empty));

        Assert.False(new ClipboardCopier(null, statuses).Copy("abc", "commit hash"));

        Assert.Equal(ClipboardCopier.NotInteractiveMessage, statuses.Log.Latest?.Text);
    }
}
