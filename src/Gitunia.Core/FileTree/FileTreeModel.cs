using Gitunia.Git.Trees.Models;

namespace Gitunia.Core.FileTree;

/// <summary>
/// The tree of a commit as nested directories, built once from the flat <c>ls-tree</c> list (directories are implied
/// by the paths) and turned into the visible rows for an expansion state and a filter. Directories come before
/// files, each group ordinal by name. A directory whose only child is a directory folds into one row
/// (<c>src/Gitunia.Core</c>). Immutable once built; safe to build on a worker thread and read on the UI thread.
/// </summary>
public sealed class FileTreeModel
{
    private const char Separator = '/';

    private readonly FileTreeNode _root = new(string.Empty, string.Empty, null);

    private FileTreeModel()
    {
    }

    /// <summary>Every file and submodule of the tree.</summary>
    public int FileCount => _root.FileCount;

    public static FileTreeModel Empty { get; } = new();

    public static FileTreeModel Build(IReadOnlyList<TreeEntry> entries)
    {
        var model = new FileTreeModel();
        var directories = new Dictionary<string, FileTreeNode>(StringComparer.Ordinal) { [string.Empty] = model._root };
        foreach (var entry in entries)
        {
            var slash = entry.Path.LastIndexOf(Separator);
            var parent = DirectoryOf(directories, slash < 0 ? string.Empty : entry.Path[..slash]);
            parent.Children.Add(new FileTreeNode(entry.Path[(slash + 1)..], entry.Path, entry) { FileCount = 1 });
        }

        Finish(model._root);

        return model;
    }

    /// <summary>The rows to show.</summary>
    /// <param name="expandedDirectories">Paths of the directories shown open (the path of the deepest directory of a
    /// folded row).</param>
    /// <param name="filter">Case-insensitive text a path must contain; empty for no filter. A filter shows every
    /// directory that holds a match, open.</param>
    public IReadOnlyList<FileTreeRow> Rows(IReadOnlySet<string> expandedDirectories, string filter)
    {
        var rows = new List<FileTreeRow>();
        var matching = filter.Length > 0 ? DirectoriesWithMatches(filter) : null;
        AddChildren(rows, _root, 0, expandedDirectories, filter, matching);

        return rows;
    }

    /// <summary>The index of the row that holds <paramref name="index"/>'s row (the nearest earlier row one level up),
    /// -1 at the top.</summary>
    public static int ParentRow(IReadOnlyList<FileTreeRow> rows, int index)
    {
        if (index < 0 || index >= rows.Count)
        {
            return -1;
        }

        for (var candidate = index - 1; candidate >= 0; candidate--)
        {
            if (rows[candidate].Depth < rows[index].Depth)
            {
                return candidate;
            }
        }

        return -1;
    }

    private static FileTreeNode DirectoryOf(Dictionary<string, FileTreeNode> directories, string path)
    {
        if (directories.TryGetValue(path, out var known))
        {
            return known;
        }

        var slash = path.LastIndexOf(Separator);
        var parent = DirectoryOf(directories, slash < 0 ? string.Empty : path[..slash]);
        var created = new FileTreeNode(path[(slash + 1)..], path, null);
        parent.Children.Add(created);
        directories[path] = created;

        return created;
    }

    private static void Finish(FileTreeNode directory)
    {
        var count = 0;
        foreach (var child in directory.Children)
        {
            if (child.IsDirectory)
            {
                Finish(child);
            }

            count += child.FileCount;
        }

        directory.FileCount = count;
        directory.Children.Sort(CompareNodes);
    }

    private static int CompareNodes(FileTreeNode left, FileTreeNode right) =>
        left.IsDirectory != right.IsDirectory
            ? left.IsDirectory ? -1 : 1
            : string.CompareOrdinal(left.Name, right.Name);

    private HashSet<string> DirectoriesWithMatches(string filter)
    {
        var directories = new HashSet<string>(StringComparer.Ordinal);
        CollectMatches(_root, filter, directories);

        return directories;
    }

    private static bool CollectMatches(FileTreeNode directory, string filter, HashSet<string> directories)
    {
        var any = false;
        foreach (var child in directory.Children)
        {
            if (child.IsDirectory ? CollectMatches(child, filter, directories) : Matches(child, filter))
            {
                any = true;
            }
        }

        if (any)
        {
            directories.Add(directory.Path);
        }

        return any;
    }

    private static bool Matches(FileTreeNode file, string filter) =>
        file.Path.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static void AddChildren(
        List<FileTreeRow> rows,
        FileTreeNode directory,
        int depth,
        IReadOnlySet<string> expanded,
        string filter,
        HashSet<string>? matching)
    {
        foreach (var child in directory.Children)
        {
            if (!child.IsDirectory)
            {
                if (matching is null || Matches(child, filter))
                {
                    rows.Add(FileRow(child, depth));
                }

                continue;
            }

            if (matching is not null && !matching.Contains(child.Path))
            {
                continue;
            }

            AddDirectory(rows, child, depth, expanded, filter, matching);
        }
    }

    private static void AddDirectory(
        List<FileTreeRow> rows,
        FileTreeNode directory,
        int depth,
        IReadOnlySet<string> expanded,
        string filter,
        HashSet<string>? matching)
    {
        var deepest = directory;
        var label = directory.Name;
        while (OnlyDirectoryChild(deepest, filter, matching) is { } only)
        {
            deepest = only;
            label = $"{label}{Separator}{only.Name}";
        }

        var isOpen = matching is not null || expanded.Contains(deepest.Path);
        rows.Add(new FileTreeRow(
            FileTreeRowKind.Directory,
            deepest.Path,
            label,
            depth,
            isOpen,
            null,
            deepest.FileCount));
        if (isOpen)
        {
            AddChildren(rows, deepest, depth + 1, expanded, filter, matching);
        }
    }

    // The single child of a directory when it is a directory (among the children a filter keeps).
    private static FileTreeNode? OnlyDirectoryChild(FileTreeNode directory, string filter, HashSet<string>? matching)
    {
        FileTreeNode? only = null;
        foreach (var child in directory.Children)
        {
            if (matching is not null && (child.IsDirectory ? !matching.Contains(child.Path) : !Matches(child, filter)))
            {
                continue;
            }

            if (only is not null || !child.IsDirectory)
            {
                return null;
            }

            only = child;
        }

        return only;
    }

    private static FileTreeRow FileRow(FileTreeNode file, int depth)
    {
        var entry = file.Entry!;
        var kind = entry.Kind switch
        {
            TreeEntryKind.Executable => FileTreeRowKind.Executable,
            TreeEntryKind.Symlink => FileTreeRowKind.Symlink,
            TreeEntryKind.Submodule => FileTreeRowKind.Submodule,
            _ => FileTreeRowKind.File,
        };

        return new FileTreeRow(kind, file.Path, file.Name, depth, false, entry.Size, 0, entry.Id);
    }
}
