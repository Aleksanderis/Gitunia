using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Pick;

/// <summary>Reads the commits a cherry-pick or revert is about to act on.</summary>
public interface IPickCommitReader
{
    /// <summary>
    /// Reads <paramref name="hashes"/> in the given order: subject and, for a merge commit, its parents with their
    /// subjects (for the mainline prompt). A hash git does not know throws.
    /// </summary>
    Task<IReadOnlyList<PickCommit>> ReadAsync(
        RepositoryLocation location,
        IReadOnlyList<string> hashes,
        CancellationToken cancellationToken);
}
