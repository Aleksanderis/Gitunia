using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

/// <summary>The production <see cref="IRepositoryFingerprintReader"/>: stats the git directory files.</summary>
public sealed class DiskFingerprintReader : IRepositoryFingerprintReader
{
    public RepositoryFingerprintGroups Read(RepositoryLocation location) => RepositoryFingerprint.Compute(location);
}
