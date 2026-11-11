using Gitunia.Git.Watching;

namespace Gitunia.App.Tabs.Models;

/// <summary>Timing of the active tab's polling (the default way a tab notices changes made outside Gitunia).</summary>
/// <param name="RefsInterval">
/// How often the cheap fingerprint of HEAD, refs, index, worktrees and operation files is read.
/// </param>
/// <param name="StatusInterval">How often the working tree status is read at least.</param>
/// <param name="MaximumStatusInterval">The back-off never lengthens the status interval beyond this.</param>
/// <param name="BackoffFactor">
/// A status refresh that took <c>t</c> postpones the next one to at least <c>t * factor</c>.
/// </param>
/// <param name="FocusThrottle">A terminal focus-in checks at once at most this often (null: 1.5 s).</param>
public sealed record PollOptions(
    TimeSpan RefsInterval,
    TimeSpan StatusInterval,
    TimeSpan MaximumStatusInterval,
    int BackoffFactor,
    TimeSpan? FocusThrottle = null)
{
    private static readonly TimeSpan DefaultFocusThrottle = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// 2 s for refs and 5 s for status; the status interval is at least five times the last refresh, up to 60 s, so a big
    /// repository is not kept busy by its own polling.
    /// </summary>
    public static PollOptions Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(60),
        5);

    /// <summary>The shortest gap between two focus-in checks.</summary>
    public TimeSpan FocusCheckGap => FocusThrottle ?? DefaultFocusThrottle;

    /// <summary>The wait before the next status refresh after one that took <paramref name="took"/>.</summary>
    public TimeSpan StatusIntervalAfter(TimeSpan took) => StatusIntervalAfter(took, 0);

    /// <summary>
    /// The wait after a refresh that took <paramref name="took"/> and followed <paramref name="failures"/> failed or timed-out
    /// ones in a row (0 when it succeeded): it doubles for each failure, up to the maximum.
    /// </summary>
    public TimeSpan StatusIntervalAfter(TimeSpan took, int failures) =>
        RefreshBackoff.Interval(StatusInterval, MaximumStatusInterval, BackoffFactor, took, failures);
}
