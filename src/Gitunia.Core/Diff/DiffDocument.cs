namespace Gitunia.Core.Diff;

/// <summary>The rows of a whole commit diff plus the anchors hunk and file navigation jumps to.</summary>
/// <param name="Rows">All rows, top to bottom.</param>
/// <param name="HunkRows">Row index of every hunk header, ascending; for a file read whole, the first row of every group of changes instead (see <see cref="DiffDocumentBuilder.Add"/>).</param>
/// <param name="FileRows">Row index of every file header, ascending; file <c>n</c> starts at <c>FileRows[n]</c>.</param>
/// <param name="FilePaths">Per file the paths it is known by (new path, then old path when different), for finding it from the file list.</param>
/// <param name="MaximumLineNumber">The largest line number in any row, so the gutter can be as wide as it needs to be.</param>
public sealed record DiffDocument(
    IReadOnlyList<DiffRow> Rows,
    IReadOnlyList<int> HunkRows,
    IReadOnlyList<int> FileRows,
    IReadOnlyList<IReadOnlyList<string>> FilePaths,
    int MaximumLineNumber)
{
    public static DiffDocument Empty { get; } = new([], [], [], [], 0);

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
