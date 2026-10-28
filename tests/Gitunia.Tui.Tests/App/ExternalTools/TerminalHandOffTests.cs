using Gitunia.Platform.Terminal;
using Gitunia.Tui.App.ExternalTools;
using Xunit;

namespace Gitunia.Tui.Tests.App.ExternalTools;

[Trait("Os", "Independent")]
public class TerminalHandOffTests
{
    private readonly List<string> _calls = [];

    [Fact]
    public void GivenAProgram_WhenRunningIt_ThenModesAreRestoredBeforeAndAfter()
    {
        var handOff = new TerminalHandOff(new RecordingModes(_calls));

        handOff.Run(() => _calls.Add("tool"));

        Assert.Equal(["restore", "tool", "restore"], _calls);
    }

    [Fact]
    public void GivenAProgramThatThrows_WhenRunningIt_ThenModesAreStillRestoredAfterwards()
    {
        var handOff = new TerminalHandOff(new RecordingModes(_calls));

        Assert.Throws<InvalidOperationException>(() => handOff.Run(Fail));

        Assert.Equal(["restore", "restore"], _calls);
    }

    private static void Fail() => throw new InvalidOperationException("tool failed");

    private sealed class RecordingModes(List<string> calls) : ITerminalModes
    {
        public bool IsSupported => true;

        public bool Capture()
        {
            calls.Add("capture");

            return true;
        }

        public void Restore() => calls.Add("restore");
    }
}
