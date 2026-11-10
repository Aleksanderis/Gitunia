namespace Gitunia.App.Updates;

/// <summary>Exit codes of <c>gitunia update</c>, so scripts can tell "newer exists" from "failed".</summary>
public static class UpdateExitCode
{
    /// <summary>Up to date, updated, or the update command was shown.</summary>
    public const int Success = 0;

    /// <summary>The update could not be checked or applied.</summary>
    public const int Failed = 1;

    /// <summary><c>--check</c> only: a newer release exists.</summary>
    public const int UpdateAvailable = 10;
}
