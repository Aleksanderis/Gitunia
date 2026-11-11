namespace Gitunia.Git.Watching.Models;

/// <summary>Timing of a <see cref="RepositoryWatcher"/>.</summary>
/// <param name="Debounce">Quiet time after the last event before a change is reported.</param>
/// <param name="MaximumDelay">A continuous stream of events is reported at least this often.</param>
/// <param name="PollInterval">How often a polling watcher looks at the repository.</param>
/// <param name="MaximumPollInterval">The polling fallback never backs off beyond this (null: 60 s).</param>
/// <param name="PollBackoffFactor">A refresh that took <c>t</c> postpones the next poll to at least <c>t * factor</c>.</param>
/// <param name="MaximumWorkingTreeDirectories">
/// Most non-ignored working tree directories one tab watches (one inotify watch each on Linux);
/// a bigger tree polls instead.
/// </param>
/// <param name="UseFileWatchers">
/// Opt-in live watching with <see cref="RepositoryWatcher"/> (config <c>watchFiles</c>). Off by default: each .NET
/// <c>FileSystemWatcher</c> takes an inotify instance, a scarce per-user resource, so tabs poll instead
/// (see <c>TabPoller</c>). Only <c>TabRefresher</c> reads this flag; <see cref="RepositoryWatcher"/> always watches.
/// </param>
public sealed record RepositoryWatcherOptions(
    TimeSpan Debounce,
    TimeSpan MaximumDelay,
    TimeSpan PollInterval,
    TimeSpan? MaximumPollInterval = null,
    int PollBackoffFactor = RepositoryWatcherOptions.DefaultPollBackoffFactor,
    int MaximumWorkingTreeDirectories = RepositoryWatcherOptions.DefaultMaximumWorkingTreeDirectories,
    bool UseFileWatchers = false)
{
    /// <summary>
    /// Linux defaults to at least 8192 watches per user, shared by every process, so a tab takes at most a quarter.
    /// </summary>
    public const int DefaultMaximumWorkingTreeDirectories = 2048;

    /// <summary>A poll waits at least five times the refresh it caused.</summary>
    public const int DefaultPollBackoffFactor = 5;

    private static readonly TimeSpan DefaultMaximumPollInterval = TimeSpan.FromSeconds(60);

    /// <summary>The longest wait the polling fallback backs off to.</summary>
    public TimeSpan PollIntervalLimit => MaximumPollInterval ?? DefaultMaximumPollInterval;

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
