using System.Globalization;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Git.Diff;

internal sealed partial class DiffFileBuilder
{
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
