using Gitunia.Core.Clipboard;
using Xunit;

namespace Gitunia.Core.Tests.Clipboard;

public class Osc52EnvironmentTests
{
    [Fact]
    public void GivenPlainTerminal_WhenDetecting_ThenDirect()
    {
        var options = Osc52Environment.Detect(Lookup(new EnvironmentVariable("TERM", "xterm-256color")));

        Assert.Equal(Osc52Mode.Direct, options.Mode);
    }

    [Fact]
    public void GivenTmuxVariable_WhenDetecting_ThenTmux()
    {
        var options = Osc52Environment.Detect(Lookup(new EnvironmentVariable("TERM", "screen-256color"), new EnvironmentVariable("TMUX", "/tmp/tmux-1000/default,1,0")));

        Assert.Equal(Osc52Mode.Tmux, options.Mode);
    }

    [Fact]
    public void GivenScreenVariable_WhenDetecting_ThenScreen()
    {
        var options = Osc52Environment.Detect(Lookup(new EnvironmentVariable("TERM", "screen"), new EnvironmentVariable("STY", "1234.pts-0.host")));

        Assert.Equal(Osc52Mode.Screen, options.Mode);
    }

    [Theory]
    [InlineData("dumb")]
    [InlineData("linux")]
    public void GivenTerminalWithoutClipboard_WhenDetecting_ThenUnsupportedWithReason(string terminal)
    {
        var options = Osc52Environment.Detect(Lookup(new EnvironmentVariable("TERM", terminal), new EnvironmentVariable("TMUX", "x")));

        Assert.Equal(Osc52Mode.Unsupported, options.Mode);
        Assert.Contains(terminal, options.UnsupportedReason, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoTermVariable_WhenDetecting_ThenDirect()
    {
        var options = Osc52Environment.Detect(Lookup());

        Assert.Equal(Osc52Mode.Direct, options.Mode);
    }

    private static Func<string, string?> Lookup(params EnvironmentVariable[] variables)
    {
        var dictionary = variables.ToDictionary(variable => variable.Name, variable => variable.Value);
        return name => dictionary.GetValueOrDefault(name);
    }
}
