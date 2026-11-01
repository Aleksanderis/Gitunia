using Gitunia.Git.Diff.Models;
using Gitunia.Tui.Loading.Diff;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>What the user picked in the Changes view diff to stage or unstage: the file and its side and the changed lines.</summary>
/// <param name="Target">The file shown, and the side it is on.</param>
/// <param name="Lines">The added and removed lines of the hunk or the Visual range; empty for a file that has no hunks (it goes whole).</param>
/// <param name="IsRange">The lines come from a Visual range, not from the hunk under the cursor.</param>
public sealed record DiffStagingRequest(WorkingDiffRequest Target, IReadOnlyList<ChangedLine> Lines, bool IsRange);
