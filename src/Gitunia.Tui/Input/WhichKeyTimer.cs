namespace Gitunia.Tui.Input;

/// <summary>
/// Runs an action on the UI thread once a chord has been pending for the which-key delay. Starting again or
/// cancelling drops the earlier wait, so a late timer callback never shows a popup for a finished chord.
/// </summary>
public sealed class WhichKeyTimer(TimeSpan delay, Action<Action> postToUi) : IDisposable
{
    private readonly object gate = new();
    private Timer? timer;
    private int generation;

    /// <summary>Waits <see cref="delay"/> (restarting any earlier wait), then runs <paramref name="elapsed"/> on the UI thread.</summary>
    public void Start(Action elapsed)
    {
        int current;
        lock (gate)
        {
            timer?.Dispose();
            current = ++generation;
            timer = new Timer(_ => postToUi(() => RunIfCurrent(current, elapsed)), null, delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Cancel()
    {
        lock (gate)
        {
            generation++;
            timer?.Dispose();
            timer = null;
        }
    }

    public void Dispose() => Cancel();

    private void RunIfCurrent(int expected, Action elapsed)
    {
        lock (gate)
        {
            if (generation != expected)
            {
                return;
            }
        }

        elapsed();
    }
}
