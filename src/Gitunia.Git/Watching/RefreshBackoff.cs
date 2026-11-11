namespace Gitunia.Git.Watching;

/// <summary>
/// How long a poller waits before reading again, from the cost of its last read (#1307). A read that took <c>t</c> postpones the
/// next one to at least <c>t * factor</c>; a failed or timed-out read counts like a slow one and the wait doubles for each failure
/// in a row, so a repository that cannot be read (a stalled network drive, a huge tree) is not kept busy by its own polling.
/// </summary>
public static class RefreshBackoff
{
    /// <summary>
    /// The wait after a read that took <paramref name="took"/> and followed <paramref name="failures"/> failed reads in a row
    /// (0 when the read succeeded); never below <paramref name="minimum"/> and never above <paramref name="maximum"/>.
    /// </summary>
    public static TimeSpan Interval(TimeSpan minimum, TimeSpan maximum, int factor, TimeSpan took, int failures)
    {
        var interval = took * factor;
        if (interval < minimum)
        {
            interval = minimum;
        }

        for (var failure = 0; failure < failures && interval < maximum; failure++)
        {
            interval *= 2;
        }

        return interval > maximum ? maximum : interval;
    }
}
