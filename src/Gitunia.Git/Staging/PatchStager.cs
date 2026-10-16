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
    private readonly PickableDiffReader _pickable = new(reader);

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
        var file = chosen.Count > 0
            ? await _pickable.ReadAsync(location, side, entry, cancellationToken).ConfigureAwait(false)
            : null;
        if (file is null)
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
