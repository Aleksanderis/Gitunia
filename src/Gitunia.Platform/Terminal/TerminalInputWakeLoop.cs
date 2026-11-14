namespace Gitunia.Platform.Terminal;

/// <summary>
/// Calls <see cref="ITerminalInputWaker.Wake"/> every interval until disposed, for the span of a shutdown that waits
/// for the toolkit's input thread. A shutdown that finishes within the first interval never wakes anything, and
/// then touches nothing; after at least one wake, disposing calls <see cref="ITerminalInputWaker.Settle"/>.
/// </summary>
public sealed class TerminalInputWakeLoop : IDisposable
{
    private readonly ITerminalInputWaker _waker;
    private readonly ManualResetEventSlim _stopped = new();
    private readonly Thread _thread;
    private int _wakeCount;

    /// <param name="waker">What ends a blocked read.</param>
    /// <param name="interval">The time between wake-ups, and before the first one.</param>
    public TerminalInputWakeLoop(ITerminalInputWaker waker, TimeSpan interval)
    {
        _waker = waker;
        _thread = new Thread(() => Run(interval))
        {
            IsBackground = true,
            Name = nameof(TerminalInputWakeLoop),
        };
        _thread.Start();
    }

    /// <summary>How many times the waker was called so far.</summary>
    public int WakeCount => Volatile.Read(ref _wakeCount);

    public void Dispose()
    {
        _stopped.Set();
        _thread.Join();
        _stopped.Dispose();
        if (WakeCount > 0)
        {
            Settle();
        }
    }

    private void Run(TimeSpan interval)
    {
        while (!_stopped.Wait(interval))
        {
            Interlocked.Increment(ref _wakeCount);
            try
            {
                _waker.Wake();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // A failed wake-up must not end the process from this thread; the next interval tries again.
            }
        }
    }

    private void Settle()
    {
        try
        {
            _waker.Settle();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Nothing is left to wake; a stray key press in the buffer is the worst outcome.
        }
    }
}
