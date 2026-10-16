using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Staging.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Discard;

/// <summary>
/// Discards chosen lines of one file from the working tree: it reads the file's unstaged diff again (git's default context, the
/// same patches <see cref="PatchStager"/> cuts), finds the chosen lines in it and applies a patch of just those changes backwards
/// to the working tree. Files with no hunks to pick from (untracked, binary, deleted, a name that is not valid UTF-8) go whole when
/// the caller allows it: tracked ones are restored from the index, untracked ones are deleted. Nothing can be undone.
/// The outcome type is the one of staging: the cases are the same.
/// </summary>
/// <param name="reader">Reads the diff of the file.</param>
/// <param name="discarder">Changes the working tree.</param>
public sealed class PatchDiscarder(IDiffReader reader, IWorkingTreeDiscarder discarder)
{
    private readonly PickableDiffReader _pickable = new(reader);

    /// <summary>Discards the chosen unstaged lines of <paramref name="entry"/>.</summary>
    /// <param name="location">The repository or worktree.</param>
    /// <param name="entry">The status entry of the file.</param>
    /// <param name="chosen">The changed lines picked in the diff; empty discards the whole file.</param>
    /// <param name="allowWholeFile">
    /// Whether a file that cannot be split may go whole. False for lines the user picked one by one:
    /// <see cref="PatchStagingOutcome.NotSplittable"/> is returned and nothing changes.
    /// </param>
    /// <param name="cancellationToken">Cancels the git calls.</param>
    public async Task<PatchStagingResult> DiscardAsync(
        RepositoryLocation location,
        StatusEntry entry,
        IReadOnlyList<ChangedLine> chosen,
        bool allowWholeFile,
        CancellationToken cancellationToken)
    {
        var file = chosen.Count > 0
            ? await _pickable
                .ReadAsync(location, WorkingDiffSide.Unstaged, entry, cancellationToken)
                .ConfigureAwait(false)
            : null;
        if (file is null)
        {
            if (chosen.Count > 0 && !allowWholeFile)
            {
                return new PatchStagingResult(PatchStagingOutcome.NotSplittable, 0);
            }

            await WholeFileAsync(location, entry, cancellationToken).ConfigureAwait(false);
            return new PatchStagingResult(PatchStagingOutcome.WholeFile, 0);
        }

        var selection = DiffLineMatcher.Match(file, chosen);
        if (selection is null)
        {
            return new PatchStagingResult(PatchStagingOutcome.FileChanged, 0);
        }

        // The patch describes the working tree as its new side, so it goes on backwards.
        var patch = DiffPatchWriter.WriteSelection(file, selection, PatchDirection.Reverse);
        if (patch.Length == 0)
        {
            return new PatchStagingResult(PatchStagingOutcome.NothingSelected, 0);
        }

        await discarder.ApplyReversePatchAsync(location, patch, cancellationToken).ConfigureAwait(false);
        return new PatchStagingResult(PatchStagingOutcome.Applied, chosen.Count);
    }

    private Task WholeFileAsync(RepositoryLocation location, StatusEntry entry, CancellationToken cancellationToken) =>
        entry.Kind == StatusEntryKind.Untracked
            ? discarder.DeleteUntrackedAsync(location, [entry.Path], cancellationToken)
            : discarder.RestoreAsync(location, [entry.Path], cancellationToken);
}
