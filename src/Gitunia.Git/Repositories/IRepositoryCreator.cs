using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Repositories;

/// <summary>
/// Creates repositories that did not exist before: clone with streamed progress and <c>git init</c>. Neither needs an
/// open repository; every path is absolute. A clone never prompts (credentials come from the user's helpers, a call
/// that needs input fails with git's message; #45 owns the real prompt) and is cancellable.
/// </summary>
public interface IRepositoryCreator
{
    /// <summary>
    /// Clones into a missing or empty directory. A failed or cancelled clone leaves nothing behind: a directory (and
    /// missing parents) this call created is removed, an empty directory that already existed is emptied again.
    /// </summary>
    /// <exception cref="CloneTargetException">
    /// The target is a file or a directory with content; nothing was touched.
    /// </exception>
    Task<RepositoryLocation> CloneAsync(
        CloneRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs <c>git init</c> in the directory (created when missing, content kept). A directory that already is a
    /// repository root is returned as it is.
    /// </summary>
    /// <exception cref="CloneTargetException">The path is a file.</exception>
    Task<InitResult> InitAsync(string directory, CancellationToken cancellationToken);
}
