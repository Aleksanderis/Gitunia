using Gitunia.Git.Diff.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.Loading.Diff;

/// <summary>Which diff the working-tree view asks for: one file of the status, or a folder of them, on one side.</summary>
/// <param name="Side">The list the file is in, so what it is compared with.</param>
/// <param name="Entry">The file's status entry (an untracked, conflicted or renamed file is read differently); the
/// first file of the folder for a folder request.</param>
/// <param name="Folder">The folder and its files for the combined diff of a folder, null for one file.</param>
public sealed record WorkingDiffRequest(WorkingDiffSide Side, StatusEntry Entry, WorkingDiffFolder? Folder = null);
