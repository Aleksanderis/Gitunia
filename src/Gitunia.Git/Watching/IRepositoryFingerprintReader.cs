using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

/// <summary>Reads the <see cref="RepositoryFingerprint"/> of a repository from disk; a seam so tests can slow it down.</summary>
public interface IRepositoryFingerprintReader
{
    RepositoryFingerprintGroups Read(RepositoryLocation location);
}
