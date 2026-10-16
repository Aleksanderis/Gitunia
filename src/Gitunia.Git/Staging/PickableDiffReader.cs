using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Staging;

/// <summary>
/// Reads the diff of one file with git's default context and turns it into something partial patches can be cut from, or null when
/// the file cannot be split into hunks and lines (binary, added, deleted, untracked, a name that is not valid UTF-8, a diff that is
/// cut short). Shared by the staging and the discarding of chosen lines, so both cut exactly the same patches.
/// </summary>
/// <param name="reader">Reads the diff of the file.</param>
public sealed class PickableDiffReader(IDiffReader reader)
{
    private const string DiffHeaderPrefix = "diff --git ";
    private const string OldSidePrefix = "a/";
    private const string NewSidePrefix = "b/";
    private const string OldPathPrefix = "--- a/";
    private const string NewPathPrefix = "+++ b/";
    private const string OldModePrefix = "old mode ";
    private const string NewModePrefix = "new mode ";
    private const char PathTerminator = '\t';

    /// <summary>The splittable diff of the file on <paramref name="side"/>, or null when it has to be handled whole.</summary>
    /// <param name="location">The repository or worktree.</param>
    /// <param name="side">Which diff to read.</param>
    /// <param name="entry">The status entry of the file.</param>
    /// <param name="cancellationToken">Cancels the git call.</param>
    /// <param name="allowAdded">
    /// Accept a new file (only meaningful for an intent-to-add entry: its lines are staged through a patch that adds some of them).
    /// Off for discarding, where lines of a new file cannot be cut back.
    /// </param>
    public async Task<DiffFile?> ReadAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        CancellationToken cancellationToken,
        bool allowAdded = false)
    {
        if (entry.Kind is not (StatusEntryKind.Ordinary or StatusEntryKind.Renamed)
            || GitPathText.HasEscapedBytes(entry.Path)
            || (entry.OriginalPath is { } original && GitPathText.HasEscapedBytes(original)))
        {
            // A name that is not valid UTF-8 gives an inexact patch, so it is handled whole by path without reading the diff.
            return null;
        }

        var read = await ReadFileAsync(location, side, entry, cancellationToken).ConfigureAwait(false);
        var file = read is null ? null : WithoutModeChange(AsEditOfNewPath(read));
        return file is not null && HasPickableHunks(file, allowAdded) ? file : null;
    }

    // Staging some lines must not also stage (or, reversed, undo) a chmod: leave the mode lines out of the patch.
    private static DiffFile? WithoutModeChange(DiffFile? file) =>
        file is null || !file.IsModeChange
            ? file
            : file with
            {
                HeaderLines =
                [
                    .. file.HeaderLines.Where(line =>
                        !line.StartsWith(OldModePrefix, StringComparison.Ordinal)
                        && !line.StartsWith(NewModePrefix, StringComparison.Ordinal)),
                ],
            };

    // A partial patch that kept the rename header would rename the file in the index when applied (and undo the rename
    // when reversed), so a renamed file with edits is cut as a plain edit of its new path: the index holds that path in
    // both cases. Null when the header cannot be rewritten (a quoted path), which sends the file through whole.
    private static DiffFile? AsEditOfNewPath(DiffFile file)
    {
        if (file.Status != DiffFileStatus.Renamed)
        {
            return file;
        }

        var newPathLine = file.HeaderLines.FirstOrDefault(line => line.StartsWith(NewPathPrefix, StringComparison.Ordinal));
        if (newPathLine is null)
        {
            return null;
        }

        // Git ends the path with a tab when it contains a space.
        var pathText = newPathLine[NewPathPrefix.Length..];
        var path = pathText.TrimEnd(PathTerminator);
        IReadOnlyList<string> header =
        [
            $"{DiffHeaderPrefix}{OldSidePrefix}{path} {NewSidePrefix}{path}",
            $"{OldPathPrefix}{pathText}",
            newPathLine,
        ];
        return file with
        {
            Status = DiffFileStatus.Modified,
            OldPath = file.NewPath,
            Similarity = null,
            OldMode = null,
            NewMode = null,
            HeaderLines = header,
        };
    }

    // Binary, added and deleted files, and a diff that is not an exact copy of the file, cannot be cut into partial patches.
    private static bool HasPickableHunks(DiffFile file, bool allowAdded) =>
        !file.IsBinary
        && !file.HasInvalidText
        && !file.IsTruncated
        && (file.Status is DiffFileStatus.Modified or DiffFileStatus.Renamed or DiffFileStatus.Copied
            || (allowAdded && file.Status == DiffFileStatus.Added))
        && file.Hunks.Count > 0;

    private async Task<DiffFile?> ReadFileAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        CancellationToken cancellationToken)
    {
        var files = reader.ReadWorkingFileAsync(location, side, entry, cancellationToken: cancellationToken);
        await foreach (var file in files.ConfigureAwait(false))
        {
            return file;
        }

        return null;
    }
}
