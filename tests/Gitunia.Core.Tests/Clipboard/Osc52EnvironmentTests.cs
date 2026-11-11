using Gitunia.Core.Clipboard;
using Xunit;

namespace Gitunia.Core.Tests.Clipboard;

public class Osc52EnvironmentTests
{
    [Theory]
    [InlineData("xterm-256color", null, null, Osc52Mode.Direct)]
    [InlineData(null, null, null, Osc52Mode.Direct)]
    [InlineData("screen-256color", "TMUX", "/tmp/tmux-1000/default,1,0", Osc52Mode.Tmux)]
    [InlineData("screen", "STY", "1234.pts-0.host", Osc52Mode.Screen)]
    public void GivenTheEnvironment_WhenDetecting_ThenTheModeFollowsTheMultiplexer(
        string? terminal,
        string? multiplexerName,
        string? multiplexerValue,
        Osc52Mode mode)
    {
        var variables = new List<EnvironmentVariable>();
        if (terminal is not null)
        {
            variables.Add(new EnvironmentVariable("TERM", terminal));
        }

        if (multiplexerName is not null)
        {
            variables.Add(new EnvironmentVariable(multiplexerName, multiplexerValue!));
        }

        var options = Osc52Environment.Detect(Lookup([.. variables]));

        Assert.Equal(mode, options.Mode);
    }

    [Theory]
    [InlineData("dumb")]
    [InlineData("linux")]
    public void GivenTerminalWithoutClipboard_WhenDetecting_ThenUnsupportedWithReason(string terminal)
    {
        var options = Osc52Environment.Detect(
            Lookup(new EnvironmentVariable("TERM", terminal), new EnvironmentVariable("TMUX", "x")));

        Assert.Equal(Osc52Mode.Unsupported, options.Mode);
        Assert.Contains(terminal, options.UnsupportedReason, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenClassicConsole_WhenDetecting_ThenUnsupportedWithReasonEvenWithoutTerm()
    {
        var options = Osc52Environment.Detect(Lookup(), isClassicConsole: true);

        Assert.Equal(Osc52Mode.Unsupported, options.Mode);
        Assert.Contains("classic Windows console", options.UnsupportedReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TERM", "xterm-256color")]
    [InlineData("TERM_PROGRAM", "vscode")]
    public void GivenClassicConsoleFlagButTerminalIdentifies_WhenDetecting_ThenDirect(string name, string value)
    {
        var options = Osc52Environment.Detect(Lookup(new EnvironmentVariable(name, value)), isClassicConsole: true);

        Assert.Equal(Osc52Mode.Direct, options.Mode);
    }

    private static Func<string, string?> Lookup(params EnvironmentVariable[] variables)
    {
        var dictionary = variables.ToDictionary(variable => variable.Name, variable => variable.Value);
        return name => dictionary.GetValueOrDefault(name);
    }
}
