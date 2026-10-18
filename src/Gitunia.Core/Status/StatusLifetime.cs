namespace Gitunia.Core.Status;

/// <summary>
/// How long a status message shows in the status bar and how long it counts as new (pure; the clock is the caller's).
/// After that the key hints come back; the history overlay keeps the message.
/// </summary>
public static class StatusLifetime
{
    /// <summary>How long a message is drawn as a filled chip to catch the eye before it settles to coloured text.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromMilliseconds(1500);

    private static readonly TimeSpan InfoLifetime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WarningLifetime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ErrorLifetime = TimeSpan.FromSeconds(30);

    /// <summary>How long a message of this severity stays: successes briefly, problems longer.</summary>
    public static TimeSpan Of(StatusSeverity severity) => severity switch
    {
        StatusSeverity.Error => ErrorLifetime,
        StatusSeverity.Warning => WarningLifetime,
        _ => InfoLifetime,
    };

    public static bool IsExpired(StatusMessage message, DateTimeOffset now) =>
        now - message.PostedAt >= Of(message.Severity);

    public static bool IsFresh(StatusMessage message, DateTimeOffset now) => now - message.PostedAt < Fresh;

    /// <summary>The next moment the message's look changes (fresh ends, then it expires), null once it is expired.</summary>
    public static DateTimeOffset? NextChange(StatusMessage message, DateTimeOffset now)
    {
        if (IsExpired(message, now))
        {
            return null;
        }

        return IsFresh(message, now) ? message.PostedAt + Fresh : message.PostedAt + Of(message.Severity);
    }
}
