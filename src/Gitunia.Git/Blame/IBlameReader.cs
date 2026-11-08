using Gitunia.Git.Blame.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Blame;

/// <summary>
/// Blames one file. The seam the blame view uses so tests can replace git. The lines and the blame are two reads: the
/// lines come first and at once, the chunks stream in while git walks the history.
/// </summary>
public interface IBlameReader
{
    /// <summary>
    /// Reads the file as it is in <paramref name="revision"/>, or in the working tree when that is null. Fails with the
    /// typed git exception when the revision or the path does not exist.
    /// </summary>
    /// <param name="location">The repository.</param>
    /// <param name="path">The path relative to the repository root, with <c>/</c> separators.</param>
    /// <param name="revision">The commit to read, null for the working tree file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<BlameContent> ReadContentAsync(
        RepositoryLocation location,
        string path,
        string? revision,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streams the runs of lines with their commits as git finds them (not in file order). Following renames is git's
    /// default. Warnings of the parser are added to <paramref name="warnings"/> once the stream ends.
    /// </summary>
    /// <param name="location">The repository.</param>
    /// <param name="path">The path relative to the repository root, with <c>/</c> separators.</param>
    /// <param name="revision">The commit to blame at, null for the working tree file.</param>
    /// <param name="options">The -w, -M and -C switches; null for none. Git itself reads <c>blame.ignoreRevsFile</c>.</param>
    /// <param name="warnings">Receives the parser's warnings.</param>
    /// <param name="cancellationToken">Cancels the stream and kills git.</param>
    IAsyncEnumerable<BlameChunk> BlameAsync(
        RepositoryLocation location,
        string path,
        string? revision,
        BlameOptions? options = null,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The value of <c>blame.ignoreRevsFile</c> in the user's git config (which git blame honors by itself), null when
    /// it is not set.
    /// </summary>
    /// <param name="location">The repository.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<string?> ReadIgnoreRevsFileAsync(RepositoryLocation location, CancellationToken cancellationToken);
}
