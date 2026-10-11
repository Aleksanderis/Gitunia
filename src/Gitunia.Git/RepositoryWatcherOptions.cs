namespace Gitunia.Git;

/// <summary>Timing of a <see cref="RepositoryWatcher"/>.</summary>
/// <param name="Debounce">Quiet time after the last event before a change is reported.</param>
/// <param name="MaximumDelay">A continuous stream of events is reported at least this often.</param>
/// <param name="PollInterval">How often a polling watcher looks at the repository.</param>
public sealed record RepositoryWatcherOptions(TimeSpan Debounce, TimeSpan MaximumDelay, TimeSpan PollInterval)
{
    public static RepositoryWatcherOptions Default { get; } = new(
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5));
}
