using Gitunia.Core.Shell;

namespace Gitunia.App.Status;

/// <summary>
/// Runs the <c>ScheduleStatusAgingEffect</c>: after the delay it posts a <c>StatusAgedCommand</c>, so the bar is drawn
/// again when the message loses its highlight or expires with no key pressed. A newer schedule replaces the earlier
/// wait; a stale timeout does nothing.
/// </summary>
/// <param name="host">Receives the command on the UI thread.</param>
public sealed class StatusAgingTimer(Dispatcher host) : IDisposable
{
    // Added to the wait, so the clock is surely past the threshold when the timeout fires.
    private static readonly TimeSpan Margin = TimeSpan.FromMilliseconds(20);

    private CancellationTokenSource? _pending;

    public void Schedule(TimeSpan after)
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        _ = WaitAsync(after + Margin, _pending.Token);
    }

    public void Dispose()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private async Task WaitAsync(TimeSpan wait, CancellationToken token)
    {
        try
        {
            await Task.Delay(wait, host.Clock, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        host.Post(new StatusAgedCommand(host.Clock.GetUtcNow()));
    }
}
