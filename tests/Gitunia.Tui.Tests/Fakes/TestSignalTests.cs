using Xunit;

namespace Gitunia.Tui.Tests.Fakes;

public class TestSignalTests
{
    [Fact]
    public void GivenADisposedSignal_WhenAnotherThreadReleasesIt_ThenNothingThrows()
    {
        var signal = new TestSignal();
        signal.Dispose();

        var exception = Record.Exception(signal.Release);

        Assert.Null(exception);
    }

    [Fact]
    public void GivenALiveSignal_WhenReleased_ThenAWaiterIsWoken()
    {
        using var signal = new TestSignal();

        signal.Release();

        Assert.True(signal.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }
}
