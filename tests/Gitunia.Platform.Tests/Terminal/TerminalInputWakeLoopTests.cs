using Gitunia.Platform.Terminal;
using Xunit;

namespace Gitunia.Platform.Tests.Terminal;

public sealed class TerminalInputWakeLoopTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    [Fact]
    public void GivenAShutdownThatWaitsForTheInputThread_WhenTheIntervalPasses_ThenTheWakerRunsRepeatedly()
    {
        var waker = new CountingWaker(2);
        var loop = new TerminalInputWakeLoop(waker, TimeSpan.FromMilliseconds(10));

        var woken = waker.Reached.Wait(Wait, TestContext.Current.CancellationToken);
        loop.Dispose();

        Assert.True(woken);
        Assert.True(waker.Count >= 2);
    }

    [Fact]
    public void GivenAShutdownThatEndsAtOnce_WhenTheLoopIsDisposed_ThenTheWakerNeverRuns()
    {
        var waker = new CountingWaker(1);
        var loop = new TerminalInputWakeLoop(waker, Wait);

        loop.Dispose();

        Assert.Equal(0, waker.Count);
    }

    private sealed class CountingWaker(int target) : ITerminalInputWaker
    {
        private int _count;

        public ManualResetEventSlim Reached { get; } = new();

        public int Count => Volatile.Read(ref _count);

        public void Wake()
        {
            if (Interlocked.Increment(ref _count) >= target)
            {
                Reached.Set();
            }
        }
    }
}
