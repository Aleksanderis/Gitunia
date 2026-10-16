using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Operations.Models;

/// <summary>What the banner of an in-progress operation shows, read from the files in the git directory.</summary>
/// <param name="Kind">The operation.</param>
/// <param name="Target">The branch being merged, or the branch being rebased; null when unknown or detached.</param>
/// <param name="Step">One-based step of a rebase, null when git does not say.</param>
/// <param name="Total">Number of steps of a rebase, null when git does not say.</param>
/// <param name="Remaining">Commits a cherry-pick or revert range still has to apply, including the stopped one; null
/// for a single commit.</param>
/// <param name="Commit">Hash of the commit being applied when the operation stopped, null for a merge or when
/// unknown.</param>
/// <param name="Subject">First line of the operation's message (the commit being applied), null when unknown.</param>
/// <param name="OriginalHead">The commit the branch pointed at before the operation; null when the files do not say
/// (HEAD itself still is it).</param>
public sealed record OperationInfo(
    RepositoryOperation Kind,
    string? Target,
    int? Step,
    int? Total,
    int? Remaining,
    string? Commit,
    string? Subject,
    string? OriginalHead);
