using Gitunia.Git.Commits.Models;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>
/// The changed files of a commit laid out as a tree of folders for the Changes tab: folders before files, each level
/// in ordinal order. A folder in the collapsed set shows no rows below it.
/// </summary>
public static class ChangesTree
{
    private const char Separator = '/';

    /// <summary>The visible rows, top to bottom.</summary>
    /// <param name="files">The changed files of the commit.</param>
    /// <param name="collapsed">Paths of the folders that are closed, null when all are open.</param>
    public static IReadOnlyList<ChangesTreeRow> Rows(
        IReadOnlyList<CommitFileChange> files,
        IReadOnlySet<string>? collapsed = null)
    {
        var root = new Node(string.Empty, string.Empty);
        for (var index = 0; index < files.Count; index++)
        {
            Insert(root, files[index].Path, index);
        }

        var rows = new List<ChangesTreeRow>();
        AddChildren(rows, root, 0, collapsed);

        return rows;
    }

    private static void Insert(Node root, string path, int fileIndex)
    {
        var node = root;
        var start = 0;
        while (path.IndexOf(Separator, start) is var slash and >= 0)
        {
            var name = path[start..slash];
            if (!node.Folders.TryGetValue(name, out var child))
            {
                child = new Node(name, path[..slash]);
                node.Folders.Add(name, child);
            }

            node = child;
            start = slash + 1;
        }

        node.Files.Add(new FileLeaf(path[start..], fileIndex));
    }

    private static void AddChildren(
        List<ChangesTreeRow> rows,
        Node node,
        int depth,
        IReadOnlySet<string>? collapsed)
    {
        foreach (var folder in node.Folders.Values)
        {
            rows.Add(new ChangesTreeRow(depth, folder.Name, folder.FolderPath, -1));
            if (collapsed is null || !collapsed.Contains(folder.FolderPath))
            {
                AddChildren(rows, folder, depth + 1, collapsed);
            }
        }

        foreach (var file in node.Files.OrderBy(leaf => leaf.Name, StringComparer.Ordinal))
        {
            rows.Add(new ChangesTreeRow(depth, file.Name, string.Empty, file.FileIndex));
        }
    }

    private sealed class Node(string name, string path)
    {
        public string Name { get; } = name;

        public string FolderPath { get; } = path;

        public SortedDictionary<string, Node> Folders { get; } = new(StringComparer.Ordinal);

        public List<FileLeaf> Files { get; } = [];
    }

    private sealed record FileLeaf(string Name, int FileIndex);
}
