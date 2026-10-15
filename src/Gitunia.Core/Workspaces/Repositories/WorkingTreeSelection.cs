namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>The selected file of the working-tree view, by identity so it survives a refresh.</summary>
/// <param name="Section">The list the file is in.</param>
/// <param name="Path">Path relative to the repository root.</param>
/// <param name="Index">Row index in the list when selected, used to land near it when the file disappears.</param>
public sealed record WorkingTreeSelection(WorkingTreeSection Section, string Path, int Index);
