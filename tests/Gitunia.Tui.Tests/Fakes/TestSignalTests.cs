using Xunit;

namespace Gitunia.Tui.Tests.Fakes;

public sealed class TestSignalTests
{
    [Fact]
    public void GivenADisposedSignal_WhenAnotherThreadReleasesIt_ThenNothingThrows()
    {
        var signal = new TestSignal();
        signal.Dispose();

        var exception = Record.Exception(signal.Release);

        Assert.Null(exception);
    }
}
