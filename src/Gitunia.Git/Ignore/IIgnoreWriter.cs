using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Ignore;

/// <summary>
/// Adds patterns to the <c>.gitignore</c> at the root of a working tree. It only appends: existing lines are never
/// changed, and the file is created when it does not exist. This is the seam the ignore action uses so tests can
/// replace the file system.
/// </summary>
public interface IIgnoreWriter
{
    /// <summary>
    /// Appends <paramref name="pattern"/> as its own line unless an identical line is already there. Throws
    /// <see cref="InvalidOperationException"/> for a bare repository (no working tree) and the usual I/O exceptions
    /// when the file cannot be read or written.
    /// </summary>
    Task<IgnoreAddOutcome> AddAsync(RepositoryLocation location, string pattern, CancellationToken cancellationToken);
}
