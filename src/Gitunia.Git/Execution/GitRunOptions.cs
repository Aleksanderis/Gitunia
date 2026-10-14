namespace Gitunia.Git.Execution;

/// <summary>Per-call settings.</summary>
/// <param name="Timeout">Overall limit for a run, or the idle limit between output chunks for a stream; null uses the runner default.</param>
/// <param name="ReadOnly">Sets <c>GIT_OPTIONAL_LOCKS=0</c> so polling never fights other git processes over locks.</param>
/// <param name="AcceptedExitCodes">Exit codes that are not errors (for example 1 from <c>diff --exit-code</c>); 0 is always accepted.</param>
public sealed record GitRunOptions(
    TimeSpan? Timeout = null,
    bool ReadOnly = false,
    IReadOnlyCollection<int>? AcceptedExitCodes = null)
{
    /// <summary>Exit code of commands such as <c>config --get</c> and <c>symbolic-ref -q</c> when nothing matches.</summary>
    public const int NoMatchExitCode = 1;

    public static readonly GitRunOptions Default = new();

    /// <summary>Shared options for read-only runs (polling and readers).</summary>
    public static readonly GitRunOptions ReadOnlyRun = new(ReadOnly: true);

    /// <summary>Read-only runs where <see cref="NoMatchExitCode"/> means "no result" rather than failure.</summary>
    public static readonly GitRunOptions ReadOnlyRunAllowingNoMatch = new(ReadOnly: true, AcceptedExitCodes: [NoMatchExitCode]);
}
