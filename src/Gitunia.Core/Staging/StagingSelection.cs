using Gitunia.Git.Diff.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Staging;

/// <summary>What the user picked in the Changes view diff to stage or unstage: the file and its side and the changed
/// lines, taken from what was on screen.</summary>
/// <param name="Side">The list the file is in: an Unstaged file is staged, a Staged one is unstaged.</param>
/// <param name="Entry">The status entry of the file.</param>
/// <param name="Lines">The added and removed lines of the hunk or the Visual range; empty for a file that has no hunks
/// (it goes whole).</param>
/// <param name="IsRange">The lines come from a Visual range, not from the hunk under the cursor.</param>
public sealed record StagingSelection(
    WorkingDiffSide Side,
    StatusEntry Entry,
    IReadOnlyList<ChangedLine> Lines,
    bool IsRange);
