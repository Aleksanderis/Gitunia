namespace Gitunia.Git;

/// <summary>Per-call settings.</summary>
/// <param name="Timeout">Overall limit for a run, or the idle limit between output chunks for a stream; null uses the runner default.</param>
/// <param name="ReadOnly">Sets <c>GIT_OPTIONAL_LOCKS=0</c> so polling never fights other git processes over locks.</param>
/// <param name="AcceptedExitCodes">Exit codes that are not errors (for example 1 from <c>diff --exit-code</c>); 0 is always accepted.</param>
public sealed record GitRunOptions(
    TimeSpan? Timeout = null,
    bool ReadOnly = false,
    IReadOnlyCollection<int>? AcceptedExitCodes = null)
{
    public static readonly GitRunOptions Default = new();
}
