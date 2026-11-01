using Gitunia.Core.FileTree;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>
/// The rows of one Changes view list. In the flat styles a row is a file and the row index is the index in the list; in
/// the tree style a row is a folder or a file, and everything that acts on rows (stage, unstage, discard, ranges, the
/// diff) goes through this map from rows to files.
/// </summary>
public sealed class WorkingRows
{
    private readonly IReadOnlyList<PathTreeRow>? _tree;
    private readonly IReadOnlyList<string> _paths;
    private readonly IReadOnlySet<string> _collapsed;
    private readonly int _flatCount;
    private Dictionary<int, int>? _rowOfEntry;

    private WorkingRows(
        IReadOnlyList<PathTreeRow>? tree,
        IReadOnlyList<string> paths,
        IReadOnlySet<string> collapsed,
        int flatCount)
    {
        _tree = tree;
        _paths = paths;
        _collapsed = collapsed;
        _flatCount = flatCount;
    }

    /// <summary>The rows are a tree of folders.</summary>
    public bool IsTree => _tree is not null;

    /// <summary>Number of rows.</summary>
    public int Count => _tree?.Count ?? _flatCount;

    /// <summary>A list shown flat: one row per path.</summary>
    /// <param name="paths">The paths of the list.</param>
    public static WorkingRows Flat(IReadOnlyList<string> paths) =>
        new(null, paths, new HashSet<string>(), paths.Count);

    /// <summary>A list shown as a tree of folders.</summary>
    /// <param name="paths">The paths of the list.</param>
    /// <param name="collapsed">The folders that are closed.</param>
    public static WorkingRows Tree(IReadOnlyList<string> paths, IReadOnlySet<string> collapsed) =>
        new(PathTree.Rows(paths, collapsed), paths, collapsed, paths.Count);

    /// <summary>The row at an index.</summary>
    public WorkingRow this[int row]
    {
        get
        {
            if (_tree is null)
            {
                return new WorkingRow(0, string.Empty, string.Empty, row);
            }

            return ToRow(_tree[row]);
        }
    }

    /// <summary>The row of a file, -1 when it is hidden under a closed folder.</summary>
    /// <param name="entryIndex">The index of the file in the list.</param>
    public int RowOfEntry(int entryIndex)
    {
        if (_tree is null)
        {
            return entryIndex >= 0 && entryIndex < _flatCount ? entryIndex : -1;
        }

        if (_rowOfEntry is null)
        {
            _rowOfEntry = [];
            for (var row = 0; row < _tree.Count; row++)
            {
                if (!_tree[row].IsFolder)
                {
                    _rowOfEntry[_tree[row].PathIndex] = row;
                }
            }
        }

        return _rowOfEntry.GetValueOrDefault(entryIndex, -1);
    }

    /// <summary>The row of a folder, -1 when there is none (flat styles, or a folder that is hidden or gone).</summary>
    /// <param name="folder">The folder path.</param>
    public int RowOfFolder(string folder)
    {
        for (var row = 0; _tree is not null && row < _tree.Count; row++)
        {
            if (_tree[row].IsFolder && _tree[row].FolderPath == folder)
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>The row of the outermost closed folder that holds <paramref name="path"/>, -1 when none is
    /// closed.</summary>
    /// <param name="path">The path of a file.</param>
    public int RowOfClosedAncestor(string path)
    {
        for (var row = 0; _tree is not null && row < _tree.Count; row++)
        {
            if (_tree[row].IsFolder && _collapsed.Contains(_tree[row].FolderPath)
                && PathTree.IsUnder(_tree[row].FolderPath, path))
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>Indexes of the files under a folder, in list order (hidden ones included).</summary>
    /// <param name="folder">The folder path.</param>
    public IReadOnlyList<int> EntriesUnder(string folder)
    {
        var entries = new List<int>();
        for (var index = 0; index < _paths.Count; index++)
        {
            if (PathTree.IsUnder(folder, _paths[index]))
            {
                entries.Add(index);
            }
        }

        return entries;
    }

    /// <summary>
    /// Indexes of the files that rows <paramref name="first"/> to <paramref name="last"/> stand for, each once, in row
    /// order: a file row is its file, a folder row all the files under it (also those hidden by a closed folder).
    /// </summary>
    /// <param name="first">The first row.</param>
    /// <param name="last">The last row.</param>
    public IReadOnlyList<int> EntriesOfRows(int first, int last)
    {
        var from = Math.Max(0, first);
        var to = Math.Min(Count - 1, last);
        var entries = new List<int>();
        var seen = new HashSet<int>();
        for (var row = from; row <= to; row++)
        {
            var current = this[row];
            IEnumerable<int> picked = current.IsFolder ? EntriesUnder(current.FolderPath) : [current.EntryIndex];
            entries.AddRange(picked.Where(seen.Add));
        }

        return entries;
    }

    /// <summary>The row of the folder that holds the file or folder at <paramref name="row"/>, -1 at the top
    /// level.</summary>
    /// <param name="row">The row.</param>
    public int ParentFolderRow(int row)
    {
        if (_tree is null || row < 0 || row >= _tree.Count)
        {
            return -1;
        }

        var current = _tree[row];
        var depth = current.Depth;
        for (var index = row - 1; depth > 0 && index >= 0; index--)
        {
            if (_tree[index].IsFolder && _tree[index].Depth == depth - 1)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The row of the file <paramref name="count"/> file rows from <paramref name="row"/> (folder rows
    /// are skipped),
    /// or <paramref name="row"/> itself when there is no such file.</summary>
    /// <param name="row">The row to start from.</param>
    /// <param name="forward">True to move down.</param>
    /// <param name="count">How many files to move over.</param>
    public int StepFile(int row, bool forward, int count)
    {
        var step = forward ? 1 : -1;
        var found = row;
        var remaining = Math.Max(1, count);
        for (var index = row + step; index >= 0 && index < Count && remaining > 0; index += step)
        {
            if (!this[index].IsFolder)
            {
                found = index;
                remaining--;
            }
        }

        return found;
    }

    private static WorkingRow ToRow(PathTreeRow row) => new(row.Depth, row.Name, row.FolderPath, row.PathIndex);
}
