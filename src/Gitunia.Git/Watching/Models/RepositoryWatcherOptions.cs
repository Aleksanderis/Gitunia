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
/// <param name="WatchDirectoriesIndividually">
/// One non-recursive watcher per non-ignored working tree directory (needs a directory lister). Null: on Linux only,
/// where inotify is per directory; elsewhere one recursive watcher that drops events under ignored directories.
/// </param>
/// <param name="WatcherRetryInterval">
/// After a watcher failed (not a buffer overflow, which keeps watching) the tab polls and tries to watch again after this
/// long (null: 60 s).
/// </param>
/// <param name="IgnoredReloadInterval">
/// How often the recursive watcher (Windows and macOS) re-reads the ignored directories to pick up a <c>core.excludesFile</c>
/// edit, which lies outside every watched directory (null: 5 minutes). A hidden tab reads once when it is shown again.
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
    bool UseFileWatchers = false,
    bool? WatchDirectoriesIndividually = null,
    TimeSpan? WatcherRetryInterval = null,
    TimeSpan? IgnoredReloadInterval = null)
{
    /// <summary>
    /// Linux defaults to at least 8192 watches per user, shared by every process, so a tab takes at most a quarter.
    /// </summary>
    public const int DefaultMaximumWorkingTreeDirectories = 2048;

    /// <summary>A poll waits at least five times the refresh it caused.</summary>
    public const int DefaultPollBackoffFactor = 5;

    private static readonly TimeSpan DefaultWatcherRetryInterval = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan DefaultIgnoredReloadInterval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan DefaultMaximumPollInterval = TimeSpan.FromSeconds(60);

    /// <summary>Whether the working tree is watched directory by directory (see <see cref="WatchDirectoriesIndividually"/>).</summary>
    public bool UsesPerDirectoryWatching => WatchDirectoriesIndividually ?? OperatingSystem.IsLinux();

    /// <summary>How long a tab polls after a watcher failure before it tries to watch again.</summary>
    public TimeSpan WatcherRetryIntervalOrDefault => WatcherRetryInterval ?? DefaultWatcherRetryInterval;

    /// <summary>The period of the recursive watcher's ignored-directory reload.</summary>
    public TimeSpan IgnoredReloadIntervalOrDefault => IgnoredReloadInterval ?? DefaultIgnoredReloadInterval;

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
