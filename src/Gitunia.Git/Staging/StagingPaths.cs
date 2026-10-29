using Gitunia.Git.Diff.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Staging;

/// <summary>The paths a whole-file stage or unstage of a status entry passes to git.</summary>
public static class StagingPaths
{
    /// <summary>
    /// The path, and for a rename being unstaged the old path too: leaving it out would keep the deletion of the old path staged.
    /// </summary>
    public static IReadOnlyList<string> Of(StatusEntry entry, WorkingDiffSide side)
    {
        var isStagedRename = side == WorkingDiffSide.Staged && entry.Kind == StatusEntryKind.Renamed;
        if (isStagedRename && entry.OriginalPath is { } original)
        {
            return [entry.Path, original];
        }

        return [entry.Path];
    }
}
