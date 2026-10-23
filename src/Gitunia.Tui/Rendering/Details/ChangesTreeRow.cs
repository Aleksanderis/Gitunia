namespace Gitunia.Tui.Rendering.Details;

/// <summary>One row of the tree style of the Changes tab: a folder or a changed file.</summary>
/// <param name="Depth">Number of folders above the row.</param>
/// <param name="Name">The folder name or the file name, without its folders.</param>
/// <param name="FolderPath">The path of the folder from the repository root; empty on a file row.</param>
/// <param name="FileIndex">Index of the file in the commit's file list, -1 on a folder row.</param>
public sealed record ChangesTreeRow(int Depth, string Name, string FolderPath, int FileIndex)
{
    /// <summary>The row is a folder.</summary>
    public bool IsFolder => FileIndex < 0;
}
