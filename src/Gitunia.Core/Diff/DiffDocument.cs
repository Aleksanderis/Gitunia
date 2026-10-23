namespace Gitunia.Core.Diff;

/// <summary>The rows of a whole commit diff plus the anchors hunk and file navigation jumps to.</summary>
/// <param name="Rows">All rows, top to bottom.</param>
/// <param name="HunkRows">Row index of every hunk header, ascending; for a file read whole, the first row of every group of changes instead (see <see cref="DiffDocumentBuilder.Add"/>).</param>
/// <param name="FileRows">Row index of every file header, ascending; file <c>n</c> starts at <c>FileRows[n]</c>.</param>
/// <param name="FilePaths">Per file the paths it is known by (new path, then old path when different), for finding it from the file list.</param>
/// <param name="MaximumLineNumber">The largest line number in any row, so the gutter can be as wide as it needs to be.</param>
/// <param name="ContentPath">The path of the file whose content (not a diff) the document shows, null for a
/// diff.</param>
public sealed record DiffDocument(
    IReadOnlyList<DiffRow> Rows,
    IReadOnlyList<int> HunkRows,
    IReadOnlyList<int> FileRows,
    IReadOnlyList<IReadOnlyList<string>> FilePaths,
    int MaximumLineNumber,
    string? ContentPath = null)
{
    public static DiffDocument Empty { get; } = new([], [], [], [], 0);

    /// <summary>The document shows the content of a file, not a diff.</summary>
    public bool IsFileContent => ContentPath is not null;

    /// <summary>A document of one remark row ("Loading...", an error, "no changes").</summary>
    public static DiffDocument Message(string text) =>
        new([new DiffRow(DiffRowKind.Note, text, null, null, -1)], [], [], [], 0);

    /// <summary>
    /// The document of one file of this one: its rows (parser warnings stay above the first file), its hunks and itself
    /// as the only file, so the pane never shows another file than the one chosen. The gutter width stays that of the
    /// whole diff.
    /// </summary>
    public DiffDocument ForFile(int fileIndex)
    {
        var first = FileRows[fileIndex];
        var end = fileIndex + 1 < FileRows.Count ? FileRows[fileIndex + 1] : Rows.Count;
        var start = fileIndex == 0 ? 0 : first;
        var rows = new List<DiffRow>(end - start);
        for (var row = start; row < end; row++)
        {
            rows.Add(Rows[row].FileIndex < 0 ? Rows[row] : Rows[row] with { FileIndex = 0 });
        }

        return new DiffDocument(
            rows,
            [.. HunkRows.Where(row => row >= start && row < end).Select(row => row - start)],
            [first - start],
            [FilePaths[fileIndex]],
            MaximumLineNumber);
    }

    /// <summary>
    /// The document of several files of this one, in the given order: their rows one after the other (parser warnings
    /// stay above the first file when it is file 0), their hunks and file anchors renumbered. The folder view of the
    /// Changes tab shows the files below a folder this way.
    /// </summary>
    /// <param name="fileIndexes">Indexes of the files, ascending and not empty.</param>
    public DiffDocument ForFiles(IReadOnlyList<int> fileIndexes)
    {
        var rows = new List<DiffRow>();
        var hunks = new List<int>();
        var files = new List<int>();
        var paths = new List<IReadOnlyList<string>>();
        for (var position = 0; position < fileIndexes.Count; position++)
        {
            var fileIndex = fileIndexes[position];
            var first = FileRows[fileIndex];
            var end = fileIndex + 1 < FileRows.Count ? FileRows[fileIndex + 1] : Rows.Count;
            var start = fileIndex == 0 ? 0 : first;
            var offset = rows.Count - start;
            for (var row = start; row < end; row++)
            {
                rows.Add(Rows[row].FileIndex < 0 ? Rows[row] : Rows[row] with { FileIndex = position });
            }

            hunks.AddRange(HunkRows.Where(row => row >= start && row < end).Select(row => row + offset));
            files.Add(first + offset);
            paths.Add(FilePaths[fileIndex]);
        }

        return new DiffDocument(rows, hunks, files, paths, MaximumLineNumber);
    }

    /// <summary>Indexes of the files whose (new) path lies below <paramref name="folder"/>, ascending.</summary>
    public IReadOnlyList<int> FileIndexesUnder(string folder)
    {
        var indexes = new List<int>();
        for (var index = 0; index < FilePaths.Count; index++)
        {
            if (FilePaths[index].Count > 0 && FileTreePath.IsUnder(folder, FilePaths[index][0]))
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    /// <summary>Index of the file shown under <paramref name="path"/> (its new or old path), or -1.</summary>
    public int FileIndexOf(string path)
    {
        for (var index = 0; index < FilePaths.Count; index++)
        {
            if (FilePaths[index].Contains(path))
            {
                return index;
            }
        }

        return -1;
    }
}
