namespace Gitunia.Platform.Terminal;

/// <summary>
/// Calls <see cref="ITerminalInputWaker.Wake"/> every interval until disposed, for the span of a shutdown that waits
/// for the toolkit's input thread. A shutdown that finishes within the first interval never wakes anything.
/// </summary>
public sealed class TerminalInputWakeLoop : IDisposable
{
    private readonly ManualResetEventSlim _stopped = new();
    private readonly Thread _thread;

    /// <param name="waker">What ends a blocked read.</param>
    /// <param name="interval">The time between wake-ups, and before the first one.</param>
    public TerminalInputWakeLoop(ITerminalInputWaker waker, TimeSpan interval)
    {
        _thread = new Thread(() => Run(waker, interval))
        {
            IsBackground = true,
            Name = nameof(TerminalInputWakeLoop),
        };
        _thread.Start();
    }

    public void Dispose()
    {
        _stopped.Set();
        _thread.Join();
        _stopped.Dispose();
    }

    private void Run(ITerminalInputWaker waker, TimeSpan interval)
    {
        while (!_stopped.Wait(interval))
        {
            waker.Wake();
        }
    }
}
