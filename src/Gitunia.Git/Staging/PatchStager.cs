using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Staging;

/// <summary>
/// Stages or unstages chosen lines of one file: it reads the file's diff again (with git's default context, so the patch is
/// exact and never depends on the options the user views the diff with), finds the chosen lines in it, writes a patch of just
/// those changes and applies it to the index. Files that have no hunks to pick from go whole when the caller allows it, which is
/// the only safe way for them. Mode changes are never part of a partial patch.
/// </summary>
/// <param name="reader">Reads the diff of the file.</param>
/// <param name="writer">Changes the index.</param>
public sealed class PatchStager(IDiffReader reader, IIndexWriter writer)
{
    private const string DiffHeaderPrefix = "diff --git ";
    private const string OldSidePrefix = "a/";
    private const string NewSidePrefix = "b/";
    private const string OldPathPrefix = "--- a/";
    private const string NewPathPrefix = "+++ b/";
    private const string OldModePrefix = "old mode ";
    private const string NewModePrefix = "new mode ";
    private const char PathTerminator = '\t';

    /// <summary>Stages (<see cref="WorkingDiffSide.Unstaged"/>) or unstages (<see cref="WorkingDiffSide.Staged"/>) the chosen lines.</summary>
    /// <param name="location">The repository or worktree.</param>
    /// <param name="side">The list the file is in: its working-tree diff is staged, its index diff is unstaged.</param>
    /// <param name="entry">The status entry of the file.</param>
    /// <param name="chosen">The changed lines picked in the diff; empty stages the whole file.</param>
    /// <param name="allowWholeFile">
    /// Whether a file that cannot be split (new, deleted, binary, copied, truncated) may go whole. False for lines the user picked
    /// one by one: <see cref="PatchStagingOutcome.NotSplittable"/> is returned and nothing changes.
    /// </param>
    /// <param name="isHeadUnborn">There is no commit yet (the whole-file unstage then removes the entry from the index).</param>
    /// <param name="cancellationToken">Cancels the git calls.</param>
    public async Task<PatchStagingResult> StageAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        IReadOnlyList<ChangedLine> chosen,
        bool allowWholeFile,
        bool isHeadUnborn,
        CancellationToken cancellationToken)
    {
        var read = chosen.Count > 0 && entry.Kind is StatusEntryKind.Ordinary or StatusEntryKind.Renamed
            ? await ReadAsync(location, side, entry, cancellationToken).ConfigureAwait(false)
            : null;
        var file = read is null ? null : WithoutModeChange(AsEditOfNewPath(read));
        if (file is null || !HasPickableHunks(file))
        {
            if (chosen.Count > 0 && !allowWholeFile)
            {
                return new PatchStagingResult(PatchStagingOutcome.NotSplittable, 0);
            }

            await WholeFileAsync(location, side, entry, isHeadUnborn, cancellationToken).ConfigureAwait(false);
            return new PatchStagingResult(PatchStagingOutcome.WholeFile, 0);
        }

        var selection = DiffLineMatcher.Match(file, chosen);
        if (selection is null)
        {
            return new PatchStagingResult(PatchStagingOutcome.FileChanged, 0);
        }

        var direction = side == WorkingDiffSide.Unstaged ? PatchDirection.Forward : PatchDirection.Reverse;
        var patch = DiffPatchWriter.WriteSelection(file, selection, direction);
        if (patch.Length == 0)
        {
            return new PatchStagingResult(PatchStagingOutcome.NothingSelected, 0);
        }

        await writer.ApplyPatchAsync(location, patch, direction, cancellationToken).ConfigureAwait(false);
        return new PatchStagingResult(PatchStagingOutcome.Applied, chosen.Count);
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
    private static bool HasPickableHunks(DiffFile file) =>
        !file.IsBinary
        && !file.HasInvalidText
        && !file.IsTruncated
        && file.Status is DiffFileStatus.Modified or DiffFileStatus.Renamed or DiffFileStatus.Copied
        && file.Hunks.Count > 0;

    private async Task<DiffFile?> ReadAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var files = reader.ReadWorkingFileAsync(location, side, entry, cancellationToken: cancellationToken);
            await foreach (var file in files.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                return file;
            }
        }
        catch (NotSupportedException)
        {
            // A path that is not valid UTF-8 cannot be given to the diff command; it is staged whole by path.
        }

        return null;
    }

    private Task WholeFileAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        bool isHeadUnborn,
        CancellationToken cancellationToken)
    {
        var paths = StagingPaths.Of(entry, side);
        return side == WorkingDiffSide.Unstaged
            ? writer.StageAsync(location, paths, cancellationToken)
            : writer.UnstageAsync(location, paths, isHeadUnborn, cancellationToken);
    }
}
