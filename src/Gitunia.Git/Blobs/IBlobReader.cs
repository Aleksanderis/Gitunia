using Gitunia.Git.Blobs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Blobs;

/// <summary>Reads the content of a blob; the seam the file content store uses so tests can replace git.</summary>
public interface IBlobReader
{
    /// <param name="location">The repository.</param>
    /// <param name="blob">The blob to read.</param>
    /// <param name="knownSize">The blob's size when the caller knows it (the tree listing has it); a blob known to be over
    /// the cap is not read at all.</param>
    /// <param name="cancellationToken">Cancels the git process.</param>
    Task<BlobContent> ReadAsync(
        RepositoryLocation location,
        ObjectId blob,
        long? knownSize = null,
        CancellationToken cancellationToken = default);
}
