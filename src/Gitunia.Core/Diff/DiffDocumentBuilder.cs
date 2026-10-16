using System.Globalization;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Core.Diff;

/// <summary>Flattens parsed <see cref="DiffFile"/>s into the rows of a <see cref="DiffDocument"/>, one file at a time so a stream can feed it.</summary>
public sealed class DiffDocumentBuilder
{
    private readonly List<DiffRow> _rows = [];
    private readonly List<int> _hunkRows = [];
    private readonly List<int> _fileRows = [];
    private readonly List<IReadOnlyList<string>> _filePaths = [];
    private int _maximumLineNumber;

    /// <summary>Rows added so far.</summary>
    public int RowCount => _rows.Count;

    /// <summary>The mark and path text of a file header, for example <c>R100 old.txt -&gt; new.txt</c>.</summary>
    public static string DescribeFile(DiffFile file)
    {
        var mark = file.Status switch
        {
            DiffFileStatus.Added => "A",
            DiffFileStatus.Deleted => "D",
            DiffFileStatus.Renamed => $"R{file.Similarity?.ToString(CultureInfo.InvariantCulture)}",
            DiffFileStatus.Copied => $"C{file.Similarity?.ToString(CultureInfo.InvariantCulture)}",
            _ => "M",
        };
        return file.Status is DiffFileStatus.Renamed or DiffFileStatus.Copied && file.OldPath is { } oldPath
            ? $"{mark} {oldPath} -> {file.Path}"
            : $"{mark} {file.Path}";
    }

    /// <param name="file">The file to add.</param>
    /// <param name="wholeFileContext">
    /// Set when the file was read whole (git then reports one hunk): the context lines the changes would have been grouped by,
    /// so hunk navigation still visits each group of changes as git's own hunks would. Null for a normal diff.
    /// </param>
    /// <param name="groupChanges">
    /// Set for the working-tree diff: every group of consecutive added or removed lines is its own anchor (the row of its first
    /// changed line), however close the hunk merged them, so staging and navigation act per group. Replaces the hunk header
    /// anchors and <paramref name="wholeFileContext"/>.
    /// </param>
    public void Add(DiffFile file, int? wholeFileContext = null, bool groupChanges = false)
    {
        var fileIndex = _fileRows.Count;
        _fileRows.Add(_rows.Count);
        _filePaths.Add(file.OldPath is { } oldPath && oldPath != file.Path ? [file.Path, oldPath] : [file.Path]);
        _rows.Add(new DiffRow(DiffRowKind.FileHeader, DescribeFile(file), null, null, fileIndex));
        AddNotes(file, fileIndex);
        foreach (var hunk in file.Hunks)
        {
            var headerRow = _rows.Count;
            if (wholeFileContext is null && !groupChanges)
            {
                _hunkRows.Add(headerRow);
            }

            _rows.Add(new DiffRow(DiffRowKind.HunkHeader, hunk.HeaderText, null, null, fileIndex));
            foreach (var line in hunk.Lines)
            {
                var kind = line.Kind switch
                {
                    DiffLineKind.Added => DiffRowKind.Added,
                    DiffLineKind.Removed => DiffRowKind.Removed,
                    _ => DiffRowKind.Context,
                };
                _rows.Add(new DiffRow(kind, line.Text, line.OldLineNumber, line.NewLineNumber, fileIndex));
                _maximumLineNumber = Math.Max(_maximumLineNumber, Math.Max(line.OldLineNumber ?? 0, line.NewLineNumber ?? 0));
                if (line.NoNewlineAtEnd)
                {
                    _rows.Add(new DiffRow(DiffRowKind.NoNewline, "No newline at end of file", null, null, fileIndex));
                }
            }

            if (groupChanges)
            {
                AddChangeGroupStarts(headerRow);
            }
            else if (wholeFileContext is { } context)
            {
                AddChangeGroupAnchors(headerRow, context);
            }
        }

        if (file.IsTruncated)
        {
            _rows.Add(Note("File cut short: only the first lines are shown", fileIndex));
        }
    }

    /// <summary>Adds a closing remark row, for example that the diff was cut short.</summary>
    public void AddTrailingNote(string text) => _rows.Add(new DiffRow(DiffRowKind.Note, text, null, null, _fileRows.Count - 1));

    /// <param name="warnings">Parser warnings; they become rows above the first file.</param>
    public DiffDocument Build(IReadOnlyCollection<string> warnings)
    {
        var all = new List<DiffRow>(warnings.Count + _rows.Count);
        all.AddRange(warnings.Select(warning => new DiffRow(DiffRowKind.Warning, warning, null, null, -1)));
        all.AddRange(_rows);
        var shift = warnings.Count;
        return new DiffDocument(
            all,
            [.. _hunkRows.Select(row => row + shift)],
            [.. _fileRows.Select(row => row + shift)],
            _filePaths,
            _maximumLineNumber);
    }

    // Changes closer than twice the context lines are one hunk in git's grouping; each group is an anchor, placed where its context starts.
    private void AddChangeGroupAnchors(int headerRow, int context)
    {
        var lastChange = headerRow;
        var hasChange = false;
        for (var row = headerRow + 1; row < _rows.Count; row++)
        {
            if (_rows[row].Kind is not (DiffRowKind.Added or DiffRowKind.Removed))
            {
                continue;
            }

            if (!hasChange || row - lastChange - 1 > 2 * context)
            {
                _hunkRows.Add(Math.Max(lastChange + 1, row - context));
            }

            hasChange = true;
            lastChange = row;
        }
    }

    // A group is a run of changed rows with no context between; the anchor is its first changed row.
    private void AddChangeGroupStarts(int headerRow)
    {
        var isInGroup = false;
        for (var row = headerRow + 1; row < _rows.Count; row++)
        {
            var kind = _rows[row].Kind;
            if (kind is DiffRowKind.Added or DiffRowKind.Removed)
            {
                if (!isInGroup)
                {
                    _hunkRows.Add(row);
                }

                isInGroup = true;
            }
            else if (kind != DiffRowKind.NoNewline)
            {
                isInGroup = false;
            }
        }
    }

    private void AddNotes(DiffFile file, int fileIndex)
    {
        if (file.IsBinary)
        {
            _rows.Add(Note("Binary file, content not shown", fileIndex));
        }

        if (file.IsModeChange)
        {
            _rows.Add(Note($"mode {file.OldMode} -> {file.NewMode}", fileIndex));
        }

        if (!file.IsBinary && file.Hunks.Count == 0)
        {
            var text = file.Status switch
            {
                DiffFileStatus.Renamed => "Renamed without content changes",
                DiffFileStatus.Copied => "Copied without content changes",
                DiffFileStatus.Added => "Empty file added",
                DiffFileStatus.Deleted => "Empty file deleted",
                _ => file.IsModeChange ? string.Empty : "No content changes",
            };
            if (text.Length > 0)
            {
                _rows.Add(Note(text, fileIndex));
            }
        }
    }

    private static DiffRow Note(string text, int fileIndex) => new(DiffRowKind.Note, text, null, null, fileIndex);
}
