using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Diff;

/// <summary>Streams diffs as <see cref="DiffFile"/>; the seam the diff store uses so tests can replace git.</summary>
public interface IDiffReader
{
    /// <param name="location">The repository.</param>
    /// <param name="command">A git command that prints a patch with its arguments, for example <c>["diff", "--cached"]</c>,
    /// <c>["diff", "HEAD", "--", "path"]</c> or <c>["show", "--format=", hash]</c>. The fixed options are inserted after the command name.</param>
    /// <param name="warnings">Receives the parser's warnings once the stream ends.</param>
    /// <param name="maximumLinesPerFile">Hunk lines kept per file; the rest are dropped and the file is marked <see cref="DiffFile.IsTruncated"/>.</param>
    /// <param name="cancellationToken">Cancels the stream and kills git.</param>
    IAsyncEnumerable<DiffFile> ReadAsync(
        RepositoryLocation location,
        IReadOnlyList<string> command,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the diff of a commit against its first parent (so merge commits match the file list), or the whole root commit
    /// when <paramref name="firstParent"/> is null.
    /// </summary>
    IAsyncEnumerable<DiffFile> ReadCommitAsync(
        RepositoryLocation location,
        ObjectId commit,
        ObjectId? firstParent,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the diff of one file of the working-tree view: <c>git diff</c> for a file in the Unstaged list (an untracked file as
    /// additions, a conflicted file as git's combined diff), <c>git diff --cached</c> for one in the Staged list. The file is
    /// found by its literal path (and, for a rename, the old path too). Throws <see cref="NotSupportedException"/> for a path
    /// that is not valid UTF-8: <c>git diff</c> reads no pathspec file.
    /// </summary>
    /// <param name="location">The repository.</param>
    /// <param name="side">The list the file is in.</param>
    /// <param name="entry">The status entry of the file.</param>
    /// <param name="warnings">Receives the parser's warnings once the stream ends.</param>
    /// <param name="maximumLinesPerFile">Hunk lines kept per file; the rest are dropped.</param>
    /// <param name="options">Context, whole file and whitespace; the defaults when null.</param>
    /// <param name="cancellationToken">Cancels the stream and kills git.</param>
    IAsyncEnumerable<DiffFile> ReadWorkingFileAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>The first parent of <paramref name="commit"/>, or null for a root commit.</summary>
    Task<ObjectId?> ReadFirstParentAsync(RepositoryLocation location, ObjectId commit, CancellationToken cancellationToken);
}
