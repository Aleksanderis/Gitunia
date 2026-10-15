namespace Gitunia.Tui.Input;

/// <summary>
/// Runs an action on the UI thread once a chord has been pending for the which-key delay. Starting again or
/// cancelling drops the earlier wait, so a late timer callback never shows a popup for a finished chord.
/// </summary>
public sealed class WhichKeyTimer(TimeSpan delay, Action<Action> postToUi) : IDisposable
{
    private readonly object _gate = new();
    private Timer? _timer;
    private int _generation;

    /// <summary>Waits the configured delay (restarting any earlier wait), then runs <paramref name="elapsed"/> on the UI thread.</summary>
    public void Start(Action elapsed)
    {
        int current;
        lock (_gate)
        {
            _timer?.Dispose();
            current = ++_generation;
            _timer = new Timer(_ => postToUi(() => RunIfCurrent(current, elapsed)), null, delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _generation++;
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void Dispose() => Cancel();

    private void RunIfCurrent(int expected, Action elapsed)
    {
        lock (_gate)
        {
            if (_generation != expected)
            {
                return;
            }
        }

        elapsed();
    }
}
