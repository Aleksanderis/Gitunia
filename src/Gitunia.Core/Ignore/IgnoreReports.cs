using Gitunia.Git.Ignore;

namespace Gitunia.Core.Ignore;

/// <summary>What the status bar says when an ignore ended.</summary>
public static class IgnoreReports
{
    /// <summary>The report of a pattern that was added, or found already in the file.</summary>
    public static string Done(string pattern, IgnoreAddOutcome outcome) => outcome switch
    {
        IgnoreAddOutcome.AlreadyListed =>
            $"{pattern} is already in {IgnoreWriter.FileName}: a later line may re-include it, or the file is tracked",
        _ => $"Added {pattern} to {IgnoreWriter.FileName}",
    };

    /// <summary>The failure of the write.</summary>
    public static string Failure(string reason) => $"Could not update {IgnoreWriter.FileName}: {reason}";
}
