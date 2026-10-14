namespace Gitunia.Git.Watching.Models;

/// <summary>Timing of a <see cref="RepositoryWatcher"/>.</summary>
/// <param name="Debounce">Quiet time after the last event before a change is reported.</param>
/// <param name="MaximumDelay">A continuous stream of events is reported at least this often.</param>
/// <param name="PollInterval">How often a polling watcher looks at the repository.</param>
public sealed record RepositoryWatcherOptions(TimeSpan Debounce, TimeSpan MaximumDelay, TimeSpan PollInterval)
{
    /// <summary>
    /// 200 ms: long enough to merge the burst of file events one git command causes, short enough to feel instant.
    /// 2 s: an upper bound so a build or checkout that never goes quiet still refreshes.
    /// 5 s: the polling fallback, a compromise between latency and the cost of a status run where watching is unavailable.
    /// </summary>
    public static RepositoryWatcherOptions Default { get; } = new(
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5));
}
