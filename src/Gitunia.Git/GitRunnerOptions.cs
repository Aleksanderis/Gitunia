namespace Gitunia.Git;

/// <summary>Settings shared by every call of one <see cref="GitRunner"/>.</summary>
/// <param name="Executable">Name or path of the git executable.</param>
/// <param name="DefaultTimeout">Timeout used when a call does not set its own.</param>
/// <param name="EnvironmentOverrides">Extra environment variables for every process (for example test isolation); null removes a variable.</param>
public sealed record GitRunnerOptions(
    string Executable = "git",
    TimeSpan? DefaultTimeout = null,
    IReadOnlyDictionary<string, string?>? EnvironmentOverrides = null)
{
    public static readonly TimeSpan FallbackTimeout = TimeSpan.FromSeconds(30);

    public TimeSpan EffectiveTimeout => DefaultTimeout ?? FallbackTimeout;
}
