namespace Gitunia.Core.FileTree;

/// <summary>
/// A list of paths laid out as a tree of folders, as the tree style of the Changes tab and of the working-tree lists
/// show it: folders before paths, each level in ordinal order. A folder in the collapsed set shows no rows below it.
/// </summary>
public static class PathTree
{
    private const char Separator = '/';

    /// <summary>The visible rows, top to bottom.</summary>
    /// <param name="paths">The paths, with <c>/</c> separators as git prints them. A path ending in a separator (an
    /// untracked directory) is a leaf that keeps its separator.</param>
    /// <param name="collapsed">Paths of the folders that are closed, null when all are open.</param>
    public static IReadOnlyList<PathTreeRow> Rows(IReadOnlyList<string> paths, IReadOnlySet<string>? collapsed = null)
    {
        var root = new Node(string.Empty, string.Empty);
        for (var index = 0; index < paths.Count; index++)
        {
            Insert(root, paths[index], index);
        }

        var rows = new List<PathTreeRow>();
        AddChildren(rows, root, 0, collapsed);

        return rows;
    }

    /// <summary>Whether <paramref name="path"/> lies in <paramref name="folder"/> (at any depth).</summary>
    /// <param name="folder">The folder path from the repository root, without a trailing separator.</param>
    /// <param name="path">The path of a file.</param>
    public static bool IsUnder(string folder, string path) =>
        path.Length > folder.Length
        && path[folder.Length] == Separator
        && path.StartsWith(folder, StringComparison.Ordinal);

    private static void Insert(Node root, string path, int pathIndex)
    {
        var node = root;
        var start = 0;
        var body = path.TrimEnd(Separator);
        while (body.IndexOf(Separator, start) is var slash and >= 0)
        {
            var name = body[start..slash];
            if (!node.Folders.TryGetValue(name, out var child))
            {
                child = new Node(name, body[..slash]);
                node.Folders.Add(name, child);
            }

            node = child;
            start = slash + 1;
        }

        node.Leaves.Add(new Leaf(path[start..], pathIndex));
    }

    private static void AddChildren(List<PathTreeRow> rows, Node node, int depth, IReadOnlySet<string>? collapsed)
    {
        foreach (var folder in node.Folders.Values)
        {
            rows.Add(new PathTreeRow(depth, folder.Name, folder.FolderPath, -1));
            if (collapsed is null || !collapsed.Contains(folder.FolderPath))
            {
                AddChildren(rows, folder, depth + 1, collapsed);
            }
        }

        foreach (var leaf in node.Leaves.OrderBy(candidate => candidate.Name, StringComparer.Ordinal))
        {
            rows.Add(new PathTreeRow(depth, leaf.Name, string.Empty, leaf.PathIndex));
        }
    }

    private sealed class Node(string name, string path)
    {
        public string Name { get; } = name;

        public string FolderPath { get; } = path;

        public SortedDictionary<string, Node> Folders { get; } = new(StringComparer.Ordinal);

        public List<Leaf> Leaves { get; } = [];
    }

    private sealed record Leaf(string Name, int PathIndex);
}
