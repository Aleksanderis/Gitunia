using System.Globalization;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Git.Diff;

/// <summary>Collects the lines of one <c>diff --git</c> section and turns them into a <see cref="DiffFile"/>.</summary>
internal sealed class DiffFileBuilder
{
    private const string NoNewlineMarker = "\\";
    private const string CombinedHunkMarker = "@@@";

    private readonly List<string> _headerLines = [];
    private readonly List<DiffHunk> _hunks = [];
    private readonly List<DiffLine> _openLines = [];
    private readonly List<string> _warnings;
    private readonly int _maximumLines;

    private string? _headerOldPath;
    private string? _headerNewPath;
    private string? _renameFrom;
    private string? _renameTo;
    private string? _minusPath;
    private string? _plusPath;
    private bool _hasMinusPlus;
    private string? _oldMode;
    private string? _newMode;
    private int? _similarity;
    private bool _isAdded;
    private bool _isDeleted;
    private bool _isCopy;
    private bool _isBinary;
    private bool _inBinaryPatch;
    private bool _skippingUnreadableHunk;
    private int _storedLineCount;
    private bool _lastLineStored;
    private bool _isTruncated;
    private bool _hasInvalidText;

    private string? _openHeaderText;
    private int _openOldStart;
    private int _openNewStart;
    private int _openOldCount;
    private int _openNewCount;
    private string _openSection = string.Empty;
    private int _remainingOld;
    private int _remainingNew;
    private int _nextOldNumber;
    private int _nextNewNumber;

    // Columns of line prefix in a combined diff (one per parent), 0 for an ordinary file.
    private int _combinedParents;

    public DiffFileBuilder(string headerLine, List<string> warnings, int maximumLines = int.MaxValue)
        : this(headerLine, warnings, maximumLines, isCombined: false)
    {
    }

    private DiffFileBuilder(string headerLine, List<string> warnings, int maximumLines, bool isCombined)
    {
        _warnings = warnings;
        _maximumLines = maximumLines;
        _headerLines.Add(headerLine);
        if (isCombined)
        {
            return;
        }

        var paths = DiffPathText.SplitHeader(headerLine["diff --git ".Length..]);
        if (paths is null)
        {
            warnings.Add($"Could not read the paths of '{headerLine}'");
        }
        else
        {
            _headerOldPath = paths.Value.OldPath;
            _headerNewPath = paths.Value.NewPath;
        }
    }

    /// <summary>True while hunk lines are still expected, so a line that looks like a new file header is hunk content.</summary>
    public bool IsInHunkBody => _openHeaderText is not null && (_remainingOld > 0 || _remainingNew > 0);

    /// <summary>
    /// Starts a combined diff (<c>diff --cc path</c>, what <c>git diff</c> prints for a conflicted file). Its lines carry one prefix
    /// column per parent; they are shown as one old/new pair: a line removed from any parent is removed, else one added to any
    /// parent is added, else context. Line numbers are those of the result.
    /// </summary>
    public static DiffFileBuilder Combined(string headerLine, List<string> warnings, int maximumLines = int.MaxValue)
    {
        var builder = new DiffFileBuilder(headerLine, warnings, maximumLines, isCombined: true);
        var path = DiffPathText.Unquote(headerLine[(headerLine.IndexOf(' ', "diff --".Length) + 1)..]);
        builder._headerOldPath = path;
        builder._headerNewPath = path;
        builder._combinedParents = 1;
        return builder;
    }

    /// <summary>Whether <paramref name="line"/> belongs to the open hunk, so the parser must not treat it as the start of the next file.</summary>
    public bool TakesBodyLine(string line) => _combinedParents > 0
        ? _openHeaderText is not null && !line.StartsWith("diff ", StringComparison.Ordinal)
        : IsInHunkBody;

    /// <summary>A line of this file was not valid UTF-8, so the decoded text is not an exact copy.</summary>
    public void MarkInvalidText() => _hasInvalidText = true;

    public void Add(string line)
    {
        if (_combinedParents > 0)
        {
            AddCombined(line);
            return;
        }

        if (IsInHunkBody)
        {
            AddHunkBodyLine(line);
            return;
        }

        if (_openHeaderText is not null && line.StartsWith(NoNewlineMarker, StringComparison.Ordinal) && _openLines.Count > 0 && _lastLineStored)
        {
            _openLines[^1] = _openLines[^1] with { NoNewlineAtEnd = true };
            return;
        }

        if (_inBinaryPatch)
        {
            _headerLines.Add(line);
            return;
        }

        if (line.StartsWith("@@ ", StringComparison.Ordinal))
        {
            StartHunk(line);
            return;
        }

        if (_skippingUnreadableHunk)
        {
            return;
        }

        if (_openHeaderText is not null)
        {
            _warnings.Add($"Ignored unexpected line after a hunk of '{_headerOldPath ?? _headerNewPath}': '{line}'");
            return;
        }

        _headerLines.Add(line);
        ParseHeaderLine(line);
    }

    public DiffFile Build()
    {
        CloseHunk();
        var status = _isAdded ? DiffFileStatus.Added
            : _isDeleted ? DiffFileStatus.Deleted
            : _renameFrom is not null && _isCopy ? DiffFileStatus.Copied
            : _renameFrom is not null ? DiffFileStatus.Renamed
            : DiffFileStatus.Modified;
        var oldPath = _renameFrom ?? (_hasMinusPlus ? _minusPath : _headerOldPath);
        var newPath = _renameTo ?? (_hasMinusPlus ? _plusPath : _headerNewPath);
        if (status == DiffFileStatus.Added)
        {
            oldPath = null;
        }
        else if (status == DiffFileStatus.Deleted)
        {
            newPath = null;
        }

        return new DiffFile(oldPath, newPath, status, _oldMode, _newMode, _similarity, _isBinary, _headerLines, _hunks) { IsTruncated = _isTruncated, HasInvalidText = _hasInvalidText };
    }

    private void AddCombined(string line)
    {
        if (line.StartsWith(CombinedHunkMarker, StringComparison.Ordinal))
        {
            StartCombinedHunk(line);
            return;
        }

        if (_openHeaderText is null)
        {
            _headerLines.Add(line);
            ParseHeaderLine(line);
            return;
        }

        if (line.StartsWith(NoNewlineMarker, StringComparison.Ordinal))
        {
            if (_openLines.Count > 0 && _lastLineStored)
            {
                _openLines[^1] = _openLines[^1] with { NoNewlineAtEnd = true };
            }

            return;
        }

        var prefixLength = Math.Min(_combinedParents, line.Length);
        var prefix = line.AsSpan(0, prefixLength);
        var text = line[prefixLength..];
        if (prefix.Contains('-'))
        {
            Store(new DiffLine(DiffLineKind.Removed, text, null, null, false));
        }
        else if (prefix.Contains('+'))
        {
            Store(new DiffLine(DiffLineKind.Added, text, null, _nextNewNumber++, false));
        }
        else
        {
            Store(new DiffLine(DiffLineKind.Context, text, null, _nextNewNumber++, false));
        }
    }

    private void StartCombinedHunk(string line)
    {
        CloseHunk();
        var parents = 0;
        while (parents < line.Length && line[parents] == '@')
        {
            parents++;
        }

        parents--;
        var closing = line.IndexOf($" {new string('@', parents + 1)}", parents + 1, StringComparison.Ordinal);
        var ranges = closing < 0 ? [] : line[(parents + 2)..closing].Split(' ');
        if (ranges.Length != parents + 1
            || ranges[0].Length < 2
            || ranges[^1].Length < 2
            || ranges[^1][0] != '+'
            || !TryParseRange(ranges[0][1..], out var oldStart, out var oldCount)
            || !TryParseRange(ranges[^1][1..], out var newStart, out var newCount))
        {
            // Keep the lines that follow as the body of an unnumbered hunk rather than as header lines.
            _warnings.Add($"Could not read the hunk header '{line}'");
            _combinedParents = Math.Max(1, parents);
            _openHeaderText = line;
            _nextNewNumber = 1;
            return;
        }

        _combinedParents = parents;
        _openHeaderText = line;
        _openOldStart = oldStart;
        _openOldCount = oldCount;
        _openNewStart = newStart;
        _openNewCount = newCount;
        var rest = line[(closing + parents + 2)..];
        _openSection = rest.StartsWith(' ') ? rest[1..] : rest;
        _nextNewNumber = newStart;
    }

    private void ParseHeaderLine(string line)
    {
        if (line.StartsWith("--- ", StringComparison.Ordinal))
        {
            _minusPath = DiffPathText.StripPrefix(PatchPath(line["--- ".Length..]), oldSide: true);
            _hasMinusPlus = true;
        }
        else if (line.StartsWith("+++ ", StringComparison.Ordinal))
        {
            _plusPath = DiffPathText.StripPrefix(PatchPath(line["+++ ".Length..]), oldSide: false);
            _hasMinusPlus = true;
        }
        else if (line.StartsWith("old mode ", StringComparison.Ordinal))
        {
            _oldMode = line["old mode ".Length..];
        }
        else if (line.StartsWith("new mode ", StringComparison.Ordinal))
        {
            _newMode = line["new mode ".Length..];
        }
        else if (line.StartsWith("new file mode ", StringComparison.Ordinal))
        {
            _isAdded = true;
            _newMode = line["new file mode ".Length..];
        }
        else if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
        {
            _isDeleted = true;
            _oldMode = line["deleted file mode ".Length..];
        }
        else if (line.StartsWith("similarity index ", StringComparison.Ordinal))
        {
            _similarity = ParsePercentage(line["similarity index ".Length..]);
        }
        else if (line.StartsWith("rename from ", StringComparison.Ordinal))
        {
            _renameFrom = DiffPathText.Unquote(line["rename from ".Length..]);
        }
        else if (line.StartsWith("rename to ", StringComparison.Ordinal))
        {
            _renameTo = DiffPathText.Unquote(line["rename to ".Length..]);
        }
        else if (line.StartsWith("copy from ", StringComparison.Ordinal))
        {
            _isCopy = true;
            _renameFrom = DiffPathText.Unquote(line["copy from ".Length..]);
        }
        else if (line.StartsWith("copy to ", StringComparison.Ordinal))
        {
            _isCopy = true;
            _renameTo = DiffPathText.Unquote(line["copy to ".Length..]);
        }
        else if (line.StartsWith("index ", StringComparison.Ordinal))
        {
            // "index <old>..<new> <mode>" names the mode only when it did not change.
            var parts = line.Split(' ');
            if (parts.Length == 3)
            {
                _oldMode ??= parts[2];
                _newMode ??= parts[2];
            }
        }
        else if (line.StartsWith("Binary files ", StringComparison.Ordinal))
        {
            _isBinary = true;
        }
        else if (line == "GIT binary patch")
        {
            _isBinary = true;
            _inBinaryPatch = true;
        }
    }

    /// <summary>Git appends a tab after paths that contain spaces so tools can find the end of the name.</summary>
    private static string PatchPath(string text) =>
        text.StartsWith('"') ? DiffPathText.Unquote(text) : text.TrimEnd('\t');

    private static int? ParsePercentage(string text)
    {
        var digits = text.TrimEnd('%');
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private void StartHunk(string line)
    {
        CloseHunk();
        if (!TryParseHunkHeader(line, out var oldStart, out var oldCount, out var newStart, out var newCount, out var section))
        {
            _warnings.Add($"Skipped hunk with an unreadable header: '{line}'");
            _skippingUnreadableHunk = true;
            return;
        }

        _skippingUnreadableHunk = false;

        _openHeaderText = line;
        _openOldStart = oldStart;
        _openNewStart = newStart;
        _openOldCount = oldCount;
        _openNewCount = newCount;
        _openSection = section;
        _remainingOld = oldCount;
        _remainingNew = newCount;
        _nextOldNumber = oldStart;
        _nextNewNumber = newStart;
    }

    private void AddHunkBodyLine(string line)
    {
        if (line.StartsWith(NoNewlineMarker, StringComparison.Ordinal))
        {
            if (_openLines.Count > 0 && _lastLineStored)
            {
                _openLines[^1] = _openLines[^1] with { NoNewlineAtEnd = true };
            }

            return;
        }

        var prefix = line.Length == 0 ? ' ' : line[0];
        var text = line.Length == 0 ? string.Empty : line[1..];
        if (line.Length == 0)
        {
            _warnings.Add($"Treated an empty line inside a hunk of '{_headerOldPath ?? _headerNewPath}' as an empty context line");
        }

        switch (prefix)
        {
            case ' ':
                Store(new DiffLine(DiffLineKind.Context, text, _nextOldNumber++, _nextNewNumber++, false));
                _remainingOld--;
                _remainingNew--;
                break;
            case '-':
                Store(new DiffLine(DiffLineKind.Removed, text, _nextOldNumber++, null, false));
                _remainingOld--;
                break;
            case '+':
                Store(new DiffLine(DiffLineKind.Added, text, null, _nextNewNumber++, false));
                _remainingNew--;
                break;
            default:
                _warnings.Add($"Hunk of '{_headerOldPath ?? _headerNewPath}' ended early at an unreadable line: '{line}'");
                _remainingOld = 0;
                _remainingNew = 0;
                break;
        }
    }

    private void Store(DiffLine line)
    {
        _lastLineStored = _storedLineCount < _maximumLines;
        if (_lastLineStored)
        {
            _openLines.Add(line);
            _storedLineCount++;
        }
        else
        {
            _isTruncated = true;
        }
    }

    private void CloseHunk()
    {
        if (_openHeaderText is null)
        {
            return;
        }

        if (_remainingOld > 0 || _remainingNew > 0)
        {
            _warnings.Add($"Hunk '{_openHeaderText}' of '{_headerOldPath ?? _headerNewPath}' ended before its line counts were reached");
        }

        _hunks.Add(new DiffHunk(_openHeaderText, _openOldStart, _openOldCount, _openNewStart, _openNewCount, _openSection, _openLines.ToArray()));
        _openLines.Clear();
        _openHeaderText = null;
        _remainingOld = 0;
        _remainingNew = 0;
    }

    private static bool TryParseHunkHeader(string line, out int oldStart, out int oldCount, out int newStart, out int newCount, out string section)
    {
        oldStart = oldCount = newStart = newCount = 0;
        section = string.Empty;
        var closing = line.IndexOf(" @@", 3, StringComparison.Ordinal);
        if (closing < 0)
        {
            return false;
        }

        var ranges = line[3..closing].Split(' ');
        if (ranges.Length != 2 || ranges[0].Length < 2 || ranges[1].Length < 2 || ranges[0][0] != '-' || ranges[1][0] != '+')
        {
            return false;
        }

        if (!TryParseRange(ranges[0][1..], out oldStart, out oldCount) || !TryParseRange(ranges[1][1..], out newStart, out newCount))
        {
            return false;
        }

        var rest = line[(closing + " @@".Length)..];
        section = rest.StartsWith(' ') ? rest[1..] : rest;
        return true;
    }

    private static bool TryParseRange(string text, out int start, out int count)
    {
        var comma = text.IndexOf(',', StringComparison.Ordinal);
        count = 1;
        if (comma < 0)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out start);
        }

        return int.TryParse(text[..comma], NumberStyles.None, CultureInfo.InvariantCulture, out start)
            && int.TryParse(text[(comma + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }
}
