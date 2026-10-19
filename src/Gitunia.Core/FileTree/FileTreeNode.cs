using Gitunia.Git.Trees.Models;

namespace Gitunia.Core.FileTree;

/// <summary>A directory or a file of a <see cref="FileTreeModel"/>.</summary>
public sealed class FileTreeNode(string name, string path, TreeEntry? entry)
{
    /// <summary>The last path segment.</summary>
    public string Name { get; } = name;

    /// <summary>The full path from the repository root.</summary>
    public string Path { get; } = path;

    /// <summary>The tree entry of a file; null for a directory.</summary>
    public TreeEntry? Entry { get; } = entry;

    /// <summary>Directories first, then files, each ordered by name (ordinal).</summary>
    public List<FileTreeNode> Children { get; } = [];

    /// <summary>Files below this node (1 for a file).</summary>
    public int FileCount { get; set; }

    public bool IsDirectory => Entry is null;
}
