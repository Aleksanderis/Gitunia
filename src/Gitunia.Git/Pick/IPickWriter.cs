using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Pick;

/// <summary>Cherry-picks or reverts commits on the checked-out branch of a worktree, without an editor.</summary>
public interface IPickWriter
{
    /// <summary>
    /// Applies <paramref name="commits"/> in the given order (the caller decides it; git keeps it in the
    /// sequencer). <paramref name="mainline"/> is the parent number that counts as the mainline of a merge commit; it
    /// is passed for the whole call. A stop on conflicts is a <see cref="PickOutcome.Conflicted"/> result; every other
    /// refusal (a dirty tree git cannot pick over, a merge commit without a mainline, an unknown commit) throws.
    /// </summary>
    Task<PickResult> PickAsync(
        RepositoryLocation location,
        PickKind kind,
        IReadOnlyList<string> commits,
        int? mainline,
        CancellationToken cancellationToken);
}
