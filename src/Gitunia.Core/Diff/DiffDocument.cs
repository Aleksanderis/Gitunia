namespace Gitunia.Core.Diff;

/// <summary>The rows of a whole commit diff plus the anchors hunk and file navigation jumps to.</summary>
/// <param name="Rows">All rows, top to bottom.</param>
/// <param name="HunkRows">Row index of every hunk header, ascending.</param>
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
