namespace Gitunia.Git.Execution;

/// <summary>Settings shared by every call of one <see cref="GitRunner"/>.</summary>
/// <param name="Executable">Name or path of the git executable.</param>
/// <param name="DefaultTimeout">Timeout used when a call does not set its own.</param>
/// <param name="EnvironmentOverrides">Extra environment variables for every process (for example test isolation); null removes a variable.</param>
/// <param name="MaximumConcurrentReads">
/// How many read-only <see cref="GitRunner.RunAsync"/> calls run at once; the rest wait their turn before their timeout
/// starts. Pollers, watchers and prefetch of many tabs then never spawn a burst of processes. Writes and streams are not
/// limited, so a user action never queues behind background reads.
/// </param>
public sealed record GitRunnerOptions(
    string Executable = "git",
    TimeSpan? DefaultTimeout = null,
    IReadOnlyDictionary<string, string?>? EnvironmentOverrides = null,
    int MaximumConcurrentReads = GitRunnerOptions.DefaultMaximumConcurrentReads)
{
    public const int DefaultMaximumConcurrentReads = 8;

    public static readonly TimeSpan FallbackTimeout = TimeSpan.FromSeconds(30);

    public TimeSpan EffectiveTimeout => DefaultTimeout ?? FallbackTimeout;
}
