namespace Gitunia.Tui.App.Tabs.Models;

/// <summary>Timing of the active tab's polling (the default way a tab notices changes made outside Gitunia).</summary>
/// <param name="RefsInterval">
/// How often the cheap fingerprint of HEAD, refs, index, worktrees and operation files is read.
/// </param>
/// <param name="StatusInterval">How often the working tree status is read at least.</param>
/// <param name="MaximumStatusInterval">The back-off never lengthens the status interval beyond this.</param>
/// <param name="BackoffFactor">
/// A status refresh that took <c>t</c> postpones the next one to at least <c>t * factor</c>.
/// </param>
public sealed record PollOptions(
    TimeSpan RefsInterval,
    TimeSpan StatusInterval,
    TimeSpan MaximumStatusInterval,
    int BackoffFactor)
{
    /// <summary>
    /// 2 s for refs and 5 s for status; the status interval is at least five times the last refresh, up to 60 s, so a big
    /// repository is not kept busy by its own polling.
    /// </summary>
    public static PollOptions Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(60),
        5);

    /// <summary>The wait before the next status refresh after one that took <paramref name="took"/>.</summary>
    public TimeSpan StatusIntervalAfter(TimeSpan took)
    {
        var backedOff = took * BackoffFactor;
        if (backedOff > MaximumStatusInterval)
        {
            return MaximumStatusInterval;
        }

        return backedOff > StatusInterval ? backedOff : StatusInterval;
    }
}
