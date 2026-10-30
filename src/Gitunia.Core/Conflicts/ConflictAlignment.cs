using Gitunia.Git.Conflicts.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// The whole file as rows shared by the base, ours and theirs panes (ux-workflows.md, "Conflict editor"): common text is the
/// same line in all three, a conflict takes as many rows as its longest version and the shorter versions are padded
/// with filler rows, so scrolling the panes together keeps related lines side by side. Built once per opened file
/// (linear in its size); a frame only reads the rows it shows.
/// </summary>
public sealed class ConflictAlignment
{
    private readonly IReadOnlyList<string>[] _ours;
    private readonly IReadOnlyList<string>[] _theirs;
    private readonly IReadOnlyList<string>[] _base;
    private readonly int[] _oursStart;
    private readonly int[] _theirsStart;
    private readonly int[] _baseStart;
    private readonly ConflictRow[] _rows;
    private readonly int[] _partFirstRow;
    private readonly int[] _partRowCount;
    private readonly int[] _hunkPart;
    private readonly int[] _hunksBeforePart;

    private ConflictAlignment(IReadOnlyList<ConflictPart> parts)
    {
        var count = parts.Count;
        _ours = new IReadOnlyList<string>[count];
        _theirs = new IReadOnlyList<string>[count];
        _base = new IReadOnlyList<string>[count];
        _oursStart = new int[count];
        _theirsStart = new int[count];
        _baseStart = new int[count];
        _partFirstRow = new int[count];
        _partRowCount = new int[count];
        _hunksBeforePart = new int[count];
        var hunkParts = new List<int>();
        var rows = new List<ConflictRow>();
        var oursNumber = 1;
        var theirsNumber = 1;
        var baseNumber = 1;
        for (var part = 0; part < count; part++)
        {
            _hunksBeforePart[part] = hunkParts.Count;
            _oursStart[part] = oursNumber;
            _theirsStart[part] = theirsNumber;
            _baseStart[part] = baseNumber;
            _partFirstRow[part] = rows.Count;
            if (parts[part].Hunk is { } hunk)
            {
                _ours[part] = Lines(hunk.Ours);
                _theirs[part] = Lines(hunk.Theirs);
                _base[part] = Lines(hunk.Base ?? string.Empty);
                var height = Math.Max(1, Math.Max(_ours[part].Count, Math.Max(_theirs[part].Count, _base[part].Count)));
                var index = hunkParts.Count;
                hunkParts.Add(part);
                for (var offset = 0; offset < height; offset++)
                {
                    rows.Add(new ConflictRow(part, offset, index));
                }

                _partRowCount[part] = height;
            }
            else
            {
                var lines = Lines(parts[part].Common ?? string.Empty);
                _ours[part] = lines;
                _theirs[part] = lines;
                _base[part] = lines;
                for (var offset = 0; offset < lines.Count; offset++)
                {
                    rows.Add(new ConflictRow(part, offset, -1));
                }

                _partRowCount[part] = lines.Count;
            }

            oursNumber += _ours[part].Count;
            theirsNumber += _theirs[part].Count;
            baseNumber += _base[part].Count;
        }

        _rows = [.. rows];
        _hunkPart = [.. hunkParts];
        LastOursNumber = oursNumber - 1;
        LastTheirsNumber = theirsNumber - 1;
        LastBaseNumber = baseNumber - 1;
    }

    /// <summary>Aligned rows in the file.</summary>
    public int RowCount => _rows.Length;

    /// <summary>Conflicts in the file.</summary>
    public int HunkCount => _hunkPart.Length;

    /// <summary>The line count of our version of the file, the widest number a gutter shows for it.</summary>
    public int LastOursNumber { get; }

    /// <summary>The line count of their version of the file.</summary>
    public int LastTheirsNumber { get; }

    /// <summary>The line count of the base version of the file.</summary>
    public int LastBaseNumber { get; }

    /// <summary>Builds the rows of a parsed file.</summary>
    public static ConflictAlignment Create(IReadOnlyList<ConflictPart> parts) => new(parts);

    /// <summary>Splits text into lines without their terminators; a trailing terminator adds no empty line.</summary>
    public static IReadOnlyList<string> Lines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (text[^1] == '\n')
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>The row at an index, clamped to the file.</summary>
    public ConflictRow RowAt(int row) => _rows.Length == 0 ? default : _rows[Math.Clamp(row, 0, _rows.Length - 1)];

    /// <summary>The conflict a row belongs to, -1 for common text.</summary>
    public int HunkAt(int row) => RowAt(row).Hunk;

    /// <summary>The first aligned row of a conflict.</summary>
    public int FirstRowOf(int hunk) => _partFirstRow[_hunkPart[hunk]];

    /// <summary>The aligned rows a conflict takes.</summary>
    public int RowCountOf(int hunk) => _partRowCount[_hunkPart[hunk]];

    /// <summary>
    /// The text of a row in a version pane, null for a filler row (the version has fewer lines than the longest one
    /// of its conflict).
    /// </summary>
    public string? TextAt(int row, ConflictPane pane)
    {
        var aligned = RowAt(row);
        var lines = LinesOf(aligned.Part, pane);

        return aligned.Offset < lines.Count ? lines[aligned.Offset] : null;
    }

    /// <summary>The line number of a row in its version of the file, null for a filler row.</summary>
    public int? NumberAt(int row, ConflictPane pane)
    {
        var aligned = RowAt(row);
        var lines = LinesOf(aligned.Part, pane);
        if (aligned.Offset >= lines.Count)
        {
            return null;
        }

        return StartOf(aligned.Part, pane) + aligned.Offset;
    }

    /// <summary>The last line number of a version pane, the widest its gutter needs.</summary>
    public int LastNumberOf(ConflictPane pane) => pane switch
    {
        ConflictPane.Ours => LastOursNumber,
        ConflictPane.Theirs => LastTheirsNumber,
        _ => LastBaseNumber,
    };

    /// <summary>
    /// The result line (zero-based) that sits beside a row, given where the conflicts are in the result: inside a
    /// conflict the same offset of its region, in common text the same offset after the region before it (never past
    /// the next region).
    /// </summary>
    /// <param name="row">The aligned row.</param>
    /// <param name="regions">Where each conflict sits in the result.</param>
    /// <param name="resultLines">Lines of the result.</param>
    public int ResultLineOf(int row, IReadOnlyList<ConflictRegion> regions, int resultLines)
    {
        var aligned = RowAt(row);
        if (aligned.Hunk >= 0)
        {
            var region = regions[aligned.Hunk];

            return region.Start + Math.Min(aligned.Offset, Math.Max(0, region.Length - 1));
        }

        var previous = _hunksBeforePart[aligned.Part] - 1;
        var start = previous >= 0 ? regions[previous].End : 0;
        var limit = previous + 1 < regions.Count ? regions[previous + 1].Start - 1 : resultLines - 1;

        return Math.Max(0, Math.Min(start + aligned.Offset, Math.Max(start, limit)));
    }

    /// <summary>The aligned row that sits beside a result line (the reverse of <see cref="ResultLineOf"/>).</summary>
    /// <param name="line">Zero-based result line.</param>
    /// <param name="regions">Where each conflict sits in the result.</param>
    public int RowOf(int line, IReadOnlyList<ConflictRegion> regions)
    {
        if (_rows.Length == 0)
        {
            return 0;
        }

        var previous = -1;
        for (var hunk = 0; hunk < regions.Count; hunk++)
        {
            var region = regions[hunk];
            if (region.Length > 0 && line >= region.Start && line < region.End)
            {
                return FirstRowOf(hunk) + Math.Min(line - region.Start, RowCountOf(hunk) - 1);
            }

            if (region.End <= line)
            {
                previous = hunk;
            }
        }

        return RowAfterRegion(previous, line, regions);
    }

    private int RowAfterRegion(int previous, int line, IReadOnlyList<ConflictRegion> regions)
    {
        var next = previous + 1;
        var partAfter = previous >= 0 ? _hunkPart[previous] + 1 : 0;
        if (partAfter >= _partFirstRow.Length)
        {
            return _rows.Length - 1;
        }

        var distance = line - (previous >= 0 ? regions[previous].End : 0);
        if (_partRowCount[partAfter] == 0 || _rows[_partFirstRow[partAfter]].Hunk >= 0)
        {
            return next < _hunkPart.Length ? FirstRowOf(next) : _rows.Length - 1;
        }

        return _partFirstRow[partAfter] + Math.Clamp(distance, 0, _partRowCount[partAfter] - 1);
    }

    private IReadOnlyList<string> LinesOf(int part, ConflictPane pane) => pane switch
    {
        ConflictPane.Ours => _ours[part],
        ConflictPane.Theirs => _theirs[part],
        _ => _base[part],
    };

    private int StartOf(int part, ConflictPane pane) => pane switch
    {
        ConflictPane.Ours => _oursStart[part],
        ConflictPane.Theirs => _theirsStart[part],
        _ => _baseStart[part],
    };
}
