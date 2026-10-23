namespace Gitunia.Core.FileTree;

/// <summary>One row of a list of paths laid out as a tree of folders: a folder or a path.</summary>
/// <param name="Depth">Number of folders above the row.</param>
/// <param name="Name">The folder name or the last segment of the path.</param>
/// <param name="FolderPath">The path of the folder from the repository root; empty on a path row.</param>
/// <param name="PathIndex">Index of the path in the list the tree was built from, -1 on a folder row.</param>
public sealed record PathTreeRow(int Depth, string Name, string FolderPath, int PathIndex)
{
    /// <summary>The row is a folder.</summary>
    public bool IsFolder => PathIndex < 0;
}
