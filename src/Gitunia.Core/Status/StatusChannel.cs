namespace Gitunia.Core.Status;

/// <summary>
/// The window's status-message channel: anything that fails or finishes in the background reports through
/// <see cref="Post"/>, and the status bar shows <see cref="Current"/>. Messages expire (see <see cref="StatusLifetime"/>)
/// by the injected clock; the host calls <see cref="Tick"/> after <see cref="UntilNextChange"/> to redraw. Not
/// thread-safe by design: background code posts through the UI-thread dispatcher it already uses for results.
/// </summary>
public sealed class StatusChannel(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public StatusLog Log { get; private set; } = StatusLog.Empty;

    /// <summary>The message the status bar shows now: the newest one unless it was dismissed or has expired.</summary>
    public StatusMessage? Current => Log.Current(_clock.GetUtcNow());

    /// <summary>Whether <see cref="Current"/> was posted so recently that it is drawn to catch the eye.</summary>
    public bool IsFresh => Current is { } message && StatusLifetime.IsFresh(message, _clock.GetUtcNow());

    /// <summary>The time until the status bar must be drawn again because the message ages; null when none is showing.</summary>
    public TimeSpan? UntilNextChange
    {
        get
        {
            var now = _clock.GetUtcNow();
            return Log.Current(now) is { } message && StatusLifetime.NextChange(message, now) is { } next
                ? next - now
                : null;
        }
    }

    /// <summary>Raised after a message was posted or dismissed, or aged (<see cref="Tick"/>).</summary>
    public event Action? Changed;

    public void Post(string text, StatusSeverity severity = StatusSeverity.Info)
    {
        if (Current is { } current && current.Text == text && current.Severity == severity)
        {
            // The same failure again (a repeated refresh) would only fill the history with copies.
            return;
        }

        Log = Log.Post(text, severity, _clock.GetUtcNow());
        Changed?.Invoke();
    }

    public void Error(string text) => Post(text, StatusSeverity.Error);

    public void Warning(string text) => Post(text, StatusSeverity.Warning);

    /// <summary>Hides the current message. Returns false when none was showing (an expired one counts as gone).</summary>
    public bool Dismiss()
    {
        if (Current is null)
        {
            return false;
        }

        Log = Log.Dismiss();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Tells listeners that time passed, so the message may have lost its highlight or expired.</summary>
    public void Tick() => Changed?.Invoke();
}
