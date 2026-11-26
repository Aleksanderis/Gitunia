namespace Gitunia.Core.Sync;

/// <summary>
/// When the background fetch of one tab runs next (ux-workflows.md, workflow 5). Pure: every method takes the time.
/// A failure doubles the wait up to <see cref="LongestBackoff"/>, a remote that wants credentials stops the timer for
/// the tab until the user fetches by hand, a success starts over.
/// </summary>
/// <param name="LastRun">When the last fetch of the tab started (or ended), or when the timer began to watch it.</param>
/// <param name="Failures">The background fetches that failed in a row.</param>
/// <param name="IsStopped">A background fetch needed credentials: the timer waits for a manual fetch.</param>
public sealed record AutoFetchSchedule(DateTimeOffset LastRun, int Failures, bool IsStopped)
{
    /// <summary>The longest wait after failures.</summary>
    public static readonly TimeSpan LongestBackoff = TimeSpan.FromMinutes(30);

    /// <summary>A schedule that has not run yet: the first fetch is one interval after <paramref name="now"/>.</summary>
    public static AutoFetchSchedule StartingAt(DateTimeOffset now) => new(now, 0, false);

    /// <summary>The wait between two fetches: the interval, doubled for each failure in a row, capped.</summary>
    public TimeSpan Delay(TimeSpan interval)
    {
        var cap = interval > LongestBackoff ? interval : LongestBackoff;
        var delay = interval;
        for (var failure = 0; failure < Failures && delay < cap; failure++)
        {
            delay *= 2;
        }

        return delay < cap ? delay : cap;
    }

    /// <summary>Whether a fetch is due at <paramref name="now"/>.</summary>
    public bool IsDue(DateTimeOffset now, TimeSpan interval) => !IsStopped && now - LastRun >= Delay(interval);

    /// <summary>The schedule after a background fetch was started.</summary>
    public AutoFetchSchedule Started(DateTimeOffset now) => this with { LastRun = now };

    /// <summary>The schedule after a fetch ended: a success starts over, a failure backs off or stops.</summary>
    public AutoFetchSchedule Ended(FetchEnded ended, DateTimeOffset now)
    {
        if (ended.Outcome == FetchOutcome.Succeeded)
        {
            return StartingAt(now);
        }

        // A fetch the user started and that failed still counts as a manual try: the timer may run again.
        if (!ended.IsBackground)
        {
            return this with { LastRun = now, IsStopped = false };
        }

        return new AutoFetchSchedule(now, Failures + 1, ended.Outcome == FetchOutcome.NeedsCredentials);
    }
}
