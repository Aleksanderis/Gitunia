using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Conflicts;

/// <summary>Writes the result of a conflict resolution into the working tree.</summary>
public interface IConflictWriter
{
    /// <summary>
    /// Replaces the content of a working tree file with <paramref name="text"/> (UTF-8, no byte order mark added; a
    /// mark that was part of the text stays). The index is not touched: marking the file resolved is a separate step.
    /// </summary>
    /// <param name="location">The repository (or linked worktree).</param>
    /// <param name="path">The file, relative to the working tree root.</param>
    /// <param name="text">The whole new content.</param>
    /// <param name="cancellationToken">Cancels the write before it starts.</param>
    /// <exception cref="ArgumentException">The path is absolute or leaves the working tree, or the repository is bare.</exception>
    Task WriteAsync(
        RepositoryLocation location,
        string path,
        string text,
        CancellationToken cancellationToken = default);
}
