using Gitunia.Platform.Terminal;
using Gitunia.Tui.App.Shell;
using Xunit;

namespace Gitunia.Tui.Tests.App.Shell;

public sealed class ApplicationDisposerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    [Fact]
    public void GivenAnApplicationWhoseDisposeWaitsForAWake_WhenDisposed_ThenTheWakerEndsTheWait()
    {
        using var woken = new ManualResetEventSlim();
        var application = new BlockedApplication(woken);
        var disposer = new ApplicationDisposer(new SignallingWaker(woken), TimeSpan.FromMilliseconds(10));

        disposer.Dispose(application);

        Assert.True(application.WaitEnded);
    }

    [Fact]
    public void GivenAnApplicationThatDisposesAtOnce_WhenDisposed_ThenNothingIsWoken()
    {
        using var woken = new ManualResetEventSlim();
        var disposer = new ApplicationDisposer(new SignallingWaker(woken), Wait);

        disposer.Dispose(new BlockedApplication(null));

        Assert.False(woken.IsSet);
    }

    private sealed class BlockedApplication(ManualResetEventSlim? woken) : IDisposable
    {
        public bool WaitEnded { get; private set; }

        public void Dispose() => WaitEnded = woken is null || woken.Wait(Wait);
    }

    private sealed class SignallingWaker(ManualResetEventSlim woken) : ITerminalInputWaker
    {
        public void Wake() => woken.Set();

        public void Settle()
        {
        }
    }
}
