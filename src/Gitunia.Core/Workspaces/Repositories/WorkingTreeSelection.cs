namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>The selected row of the working-tree view, by identity so it survives a refresh.</summary>
/// <param name="Section">The list the row is in.</param>
/// <param name="Path">Path relative to the repository root: the file, or the folder when
/// <paramref name="IsFolder"/>.</param>
/// <param name="Index">Row index in the list when selected, used to land near it when the row disappears.</param>
/// <param name="IsFolder">The row is a folder of the tree style, not a file.</param>
public sealed record WorkingTreeSelection(WorkingTreeSection Section, string Path, int Index, bool IsFolder = false);
