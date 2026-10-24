using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Git.Stash;

/// <summary>Reads what is inside a stash; the seam the stash view uses so tests can replace git.</summary>
public interface IStashDetailsReader
{
    /// <summary>
    /// Reads the files of <paramref name="entry"/>, found by its commit hash: the tracked changes
    /// (<c>stash^1..stash</c>, what <c>git stash show --name-status</c> prints) followed by the untracked files of the
    /// third parent when the stash was made with <c>--include-untracked</c>.
    /// </summary>
    /// <param name="location">The repository.</param>
    /// <param name="entry">The stash to read.</param>
    /// <param name="warnings">Receives the parsers' warnings.</param>
    /// <param name="cancellationToken">Cancels git.</param>
    /// <returns>The files; null when the stash is no longer in the stack.</returns>
    Task<StashFiles?> ReadFilesAsync(
        RepositoryLocation location,
        StashEntry entry,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the diff of one part of a stash: the tracked changes (the text of <c>git stash show -p</c>) or the
    /// untracked files as additions. The stash commit is addressed by hash, so this works as long as the commit
    /// exists; a commit git no longer has fails with a git error. A stash without untracked files streams nothing
    /// for <see cref="StashFileOrigin.Untracked"/>.
    /// </summary>
    /// <param name="location">The repository.</param>
    /// <param name="entry">The stash to read.</param>
    /// <param name="origin">Which part to stream.</param>
    /// <param name="warnings">Receives the parser's warnings once the stream ends.</param>
    /// <param name="maximumLinesPerFile">Hunk lines kept per file; the rest are dropped.</param>
    /// <param name="options">Context, whole file and whitespace; the defaults when null.</param>
    /// <param name="cancellationToken">Cancels the stream and kills git.</param>
    IAsyncEnumerable<DiffFile> ReadDiffAsync(
        RepositoryLocation location,
        StashEntry entry,
        StashFileOrigin origin,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default);
}
