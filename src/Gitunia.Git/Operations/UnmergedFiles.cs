using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Operations;

/// <summary>
/// The unmerged index entries of a worktree (<c>git ls-files --unmerged</c>). A write that fails counts as "stopped on
/// conflicts" only when this set changed to a non-empty one; files that were already conflicted mean git refused the call.
/// </summary>
internal static class UnmergedFiles
{
    private static readonly string[] Arguments = ["ls-files", "--unmerged"];

    /// <summary>The raw listing; equal listings mean the same conflicts.</summary>
    public static async Task<string> ReadAsync(
        GitRunner runner,
        RepositoryLocation location,
        CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, Arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);

        return result.StandardOutputText;
    }

    /// <summary>True when the call that turned <paramref name="before"/> into <paramref name="after"/> made conflicts.</summary>
    public static bool MadeConflicts(string before, string after) => after.Length > 0 && after != before;
}
