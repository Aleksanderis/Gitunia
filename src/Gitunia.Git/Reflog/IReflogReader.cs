using Gitunia.Git.Reflog.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Reflog;

/// <summary>
/// Reads the reflog of <c>HEAD</c> or of a branch. This is the seam the reflog view uses so tests can replace git.
/// </summary>
public interface IReflogReader
{
    /// <summary>
    /// Reads at most <paramref name="limit"/> entries of <paramref name="reference"/> (<c>HEAD</c> or a full ref name),
    /// newest first. Fails with the typed git exception when the ref has no reflog or does not exist.
    /// </summary>
    Task<ParseResult<ReflogEntry>> ReadAsync(
        RepositoryLocation location,
        string reference,
        int limit,
        CancellationToken cancellationToken);
}
