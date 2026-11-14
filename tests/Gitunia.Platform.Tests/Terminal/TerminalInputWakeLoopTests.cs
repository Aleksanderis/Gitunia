using Gitunia.Platform.Terminal;
using Xunit;

namespace Gitunia.Platform.Tests.Terminal;

public sealed class TerminalInputWakeLoopTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(10);

    [Fact]
    public void GivenAShutdownThatWaitsForTheInputThread_WhenTheIntervalPasses_ThenTheWakerRunsRepeatedly()
    {
        var waker = new CountingWaker(2);
        var loop = new TerminalInputWakeLoop(waker, Interval);

        var woken = waker.Reached.Wait(Wait, TestContext.Current.CancellationToken);
        loop.Dispose();

        Assert.True(woken);
        Assert.True(waker.WakeCount >= 2);
    }

    [Fact]
    public void GivenAShutdownThatEndsAtOnce_WhenTheLoopIsDisposed_ThenNothingIsWokenOrSettled()
    {
        var waker = new CountingWaker(1);
        var loop = new TerminalInputWakeLoop(waker, Wait);

        loop.Dispose();

        Assert.Equal(0, waker.WakeCount);
        Assert.Equal(0, waker.SettleCount);
    }

    [Fact]
    public void GivenAWakeThatRan_WhenTheLoopIsDisposed_ThenTheInputIsSettledOnceAfterTheLastWake()
    {
        var waker = new CountingWaker(1);
        var loop = new TerminalInputWakeLoop(waker, Interval);
        Assert.True(waker.Reached.Wait(Wait, TestContext.Current.CancellationToken));

        loop.Dispose();

        Assert.Equal(1, waker.SettleCount);
        Assert.Equal(waker.WakeCountAtSettle, waker.WakeCount);
    }

    [Fact]
    public void GivenADisposedLoop_WhenSeveralIntervalsPass_ThenTheWakeCountStaysTheSame()
    {
        var waker = new CountingWaker(1);
        var loop = new TerminalInputWakeLoop(waker, Interval);
        Assert.True(waker.Reached.Wait(Wait, TestContext.Current.CancellationToken));
        loop.Dispose();
        var countAtDispose = waker.WakeCount;

        // Only this wait proves that nothing runs after the dispose; nothing can be waited for.
        TestContext.Current.CancellationToken.WaitHandle.WaitOne(Interval * 5);

        Assert.Equal(countAtDispose, waker.WakeCount);
    }

    [Fact]
    public void GivenAWakerThatThrows_WhenTheLoopRuns_ThenItKeepsWakingAndSettlesWithoutThrowing()
    {
        var waker = new CountingWaker(3) { Throws = true };
        var loop = new TerminalInputWakeLoop(waker, Interval);
        var woken = waker.Reached.Wait(Wait, TestContext.Current.CancellationToken);

        var exception = Record.Exception(loop.Dispose);

        Assert.True(woken);
        Assert.Null(exception);
        Assert.Equal(1, waker.SettleCount);
    }

    private sealed class CountingWaker(int target) : ITerminalInputWaker
    {
        private int _wakeCount;
        private int _settleCount;

        public ManualResetEventSlim Reached { get; } = new();

        public bool Throws { get; init; }

        public int WakeCount => Volatile.Read(ref _wakeCount);

        public int SettleCount => Volatile.Read(ref _settleCount);

        public int WakeCountAtSettle { get; private set; }

        public void Wake()
        {
            if (Interlocked.Increment(ref _wakeCount) >= target)
            {
                Reached.Set();
            }

            if (Throws)
            {
                throw new InvalidOperationException("The console is gone.");
            }
        }

        public void Settle()
        {
            WakeCountAtSettle = WakeCount;
            Interlocked.Increment(ref _settleCount);
            if (Throws)
            {
                throw new InvalidOperationException("The console is gone.");
            }
        }
    }
}
