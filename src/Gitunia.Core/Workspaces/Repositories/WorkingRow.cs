namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>One row of a Changes view list: a file, or in the tree style a folder.</summary>
/// <param name="Depth">Number of folders above the row, 0 in the flat styles.</param>
/// <param name="Name">The folder name or the last segment of the path; empty in the flat styles.</param>
/// <param name="FolderPath">The path of the folder from the repository root, empty on a file row.</param>
/// <param name="EntryIndex">Index of the file in the list of entries, -1 on a folder row.</param>
public sealed record WorkingRow(int Depth, string Name, string FolderPath, int EntryIndex)
{
    /// <summary>The row is a folder.</summary>
    public bool IsFolder => EntryIndex < 0;
}
