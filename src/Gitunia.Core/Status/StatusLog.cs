namespace Gitunia.Core.Status;

/// <summary>
/// The status messages of the window as an immutable value: a bounded history plus which message is still showing in
/// the status bar. Transitions return a new log; the shell state holds the current one.
/// </summary>
public sealed record StatusLog
{
    /// <summary>How many messages the history keeps.</summary>
    public const int HistoryCapacity = 50;

    private StatusLog(IReadOnlyList<StatusMessage> history, long dismissedThrough)
    {
        History = history;
        DismissedThrough = dismissedThrough;
    }

    public static StatusLog Empty { get; } = new([], 0);

    /// <summary>Messages oldest first, at most <see cref="HistoryCapacity"/>; dismissed ones stay.</summary>
    public IReadOnlyList<StatusMessage> History { get; }

    /// <summary>Messages up to this sequence number were dismissed.</summary>
    public long DismissedThrough { get; }

    /// <summary>The message to show in the status bar: the newest one unless it was dismissed.</summary>
    public StatusMessage? Latest => History.Count > 0 && History[^1].Sequence > DismissedThrough ? History[^1] : null;

    /// <summary>The message to show at <paramref name="now"/>: <see cref="Latest"/> unless it has expired.</summary>
    public StatusMessage? Current(DateTimeOffset now) =>
        Latest is { } latest && !StatusLifetime.IsExpired(latest, now) ? latest : null;

    public StatusLog Post(string text, StatusSeverity severity, DateTimeOffset postedAt, string? detail = null)
    {
        text = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var sequence = History.Count > 0 ? History[^1].Sequence + 1 : DismissedThrough + 1;
        var kept = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        var history = new List<StatusMessage>(History) { new(sequence, text, severity, postedAt, kept) };
        if (history.Count > HistoryCapacity)
        {
            history.RemoveRange(0, history.Count - HistoryCapacity);
        }

        return new StatusLog(history, DismissedThrough);
    }

    /// <summary>Hides the current message; the history keeps it. Does nothing when none is showing.</summary>
    public StatusLog Dismiss() => Latest is { } latest ? new StatusLog(History, latest.Sequence) : this;
}
