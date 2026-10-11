using System.Globalization;

namespace Gitunia.Git;

/// <summary>Collects the lines of one <c>diff --git</c> section and turns them into a <see cref="DiffFile"/>.</summary>
internal sealed class DiffFileBuilder
{
    private const string NoNewlineMarker = "\\";

    private readonly List<string> headerLines = [];
    private readonly List<DiffHunk> hunks = [];
    private readonly List<DiffLine> openLines = [];
    private readonly List<string> warnings;

    private string? headerOldPath;
    private string? headerNewPath;
    private string? renameFrom;
    private string? renameTo;
    private string? minusPath;
    private string? plusPath;
    private bool hasMinusPlus;
    private string? oldMode;
    private string? newMode;
    private int? similarity;
    private bool isAdded;
    private bool isDeleted;
    private bool isCopy;
    private bool isBinary;
    private bool inBinaryPatch;
    private bool skippingUnreadableHunk;

    private string? openHeaderText;
    private int openOldStart;
    private int openNewStart;
    private int openOldCount;
    private int openNewCount;
    private string openSection = string.Empty;
    private int remainingOld;
    private int remainingNew;
    private int nextOldNumber;
    private int nextNewNumber;

    public DiffFileBuilder(string headerLine, List<string> warnings)
    {
        this.warnings = warnings;
        headerLines.Add(headerLine);
        var paths = DiffPathText.SplitHeader(headerLine["diff --git ".Length..]);
        if (paths is null)
        {
            warnings.Add($"Could not read the paths of '{headerLine}'");
        }
        else
        {
            headerOldPath = paths.Value.OldPath;
            headerNewPath = paths.Value.NewPath;
        }
    }

    /// <summary>True while hunk lines are still expected, so a line that looks like a new file header is hunk content.</summary>
    public bool IsInHunkBody => openHeaderText is not null && (remainingOld > 0 || remainingNew > 0);

    public void Add(string line)
    {
        if (IsInHunkBody)
        {
            AddHunkBodyLine(line);
            return;
        }

        if (openHeaderText is not null && line.StartsWith(NoNewlineMarker, StringComparison.Ordinal) && openLines.Count > 0)
        {
            openLines[^1] = openLines[^1] with { NoNewlineAtEnd = true };
            return;
        }

        if (inBinaryPatch)
        {
            headerLines.Add(line);
            return;
        }

        if (line.StartsWith("@@ ", StringComparison.Ordinal))
        {
            StartHunk(line);
            return;
        }

        if (skippingUnreadableHunk)
        {
            return;
        }

        if (openHeaderText is not null)
        {
            warnings.Add($"Ignored unexpected line after a hunk of '{headerOldPath ?? headerNewPath}': '{line}'");
            return;
        }

        headerLines.Add(line);
        ParseHeaderLine(line);
    }

    public DiffFile Build()
    {
        CloseHunk();
        var status = isAdded ? DiffFileStatus.Added
            : isDeleted ? DiffFileStatus.Deleted
            : renameFrom is not null && isCopy ? DiffFileStatus.Copied
            : renameFrom is not null ? DiffFileStatus.Renamed
            : DiffFileStatus.Modified;
        var oldPath = renameFrom ?? (hasMinusPlus ? minusPath : headerOldPath);
        var newPath = renameTo ?? (hasMinusPlus ? plusPath : headerNewPath);
        if (status == DiffFileStatus.Added)
        {
            oldPath = null;
        }
        else if (status == DiffFileStatus.Deleted)
        {
            newPath = null;
        }

        return new DiffFile(oldPath, newPath, status, oldMode, newMode, similarity, isBinary, headerLines, hunks);
    }

    private void ParseHeaderLine(string line)
    {
        if (line.StartsWith("--- ", StringComparison.Ordinal))
        {
            minusPath = DiffPathText.StripPrefix(PatchPath(line["--- ".Length..]), oldSide: true);
            hasMinusPlus = true;
        }
        else if (line.StartsWith("+++ ", StringComparison.Ordinal))
        {
            plusPath = DiffPathText.StripPrefix(PatchPath(line["+++ ".Length..]), oldSide: false);
            hasMinusPlus = true;
        }
        else if (line.StartsWith("old mode ", StringComparison.Ordinal))
        {
            oldMode = line["old mode ".Length..];
        }
        else if (line.StartsWith("new mode ", StringComparison.Ordinal))
        {
            newMode = line["new mode ".Length..];
        }
        else if (line.StartsWith("new file mode ", StringComparison.Ordinal))
        {
            isAdded = true;
            newMode = line["new file mode ".Length..];
        }
        else if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
        {
            isDeleted = true;
            oldMode = line["deleted file mode ".Length..];
        }
        else if (line.StartsWith("similarity index ", StringComparison.Ordinal))
        {
            similarity = ParsePercentage(line["similarity index ".Length..]);
        }
        else if (line.StartsWith("rename from ", StringComparison.Ordinal))
        {
            renameFrom = DiffPathText.Unquote(line["rename from ".Length..]);
        }
        else if (line.StartsWith("rename to ", StringComparison.Ordinal))
        {
            renameTo = DiffPathText.Unquote(line["rename to ".Length..]);
        }
        else if (line.StartsWith("copy from ", StringComparison.Ordinal))
        {
            isCopy = true;
            renameFrom = DiffPathText.Unquote(line["copy from ".Length..]);
        }
        else if (line.StartsWith("copy to ", StringComparison.Ordinal))
        {
            isCopy = true;
            renameTo = DiffPathText.Unquote(line["copy to ".Length..]);
        }
        else if (line.StartsWith("index ", StringComparison.Ordinal))
        {
            // "index <old>..<new> <mode>" names the mode only when it did not change.
            var parts = line.Split(' ');
            if (parts.Length == 3)
            {
                oldMode ??= parts[2];
                newMode ??= parts[2];
            }
        }
        else if (line.StartsWith("Binary files ", StringComparison.Ordinal))
        {
            isBinary = true;
        }
        else if (line == "GIT binary patch")
        {
            isBinary = true;
            inBinaryPatch = true;
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
            warnings.Add($"Skipped hunk with an unreadable header: '{line}'");
            skippingUnreadableHunk = true;
            return;
        }

        skippingUnreadableHunk = false;

        openHeaderText = line;
        openOldStart = oldStart;
        openNewStart = newStart;
        openOldCount = oldCount;
        openNewCount = newCount;
        openSection = section;
        remainingOld = oldCount;
        remainingNew = newCount;
        nextOldNumber = oldStart;
        nextNewNumber = newStart;
    }

    private void AddHunkBodyLine(string line)
    {
        if (line.StartsWith(NoNewlineMarker, StringComparison.Ordinal))
        {
            if (openLines.Count > 0)
            {
                openLines[^1] = openLines[^1] with { NoNewlineAtEnd = true };
            }

            return;
        }

        var prefix = line.Length == 0 ? ' ' : line[0];
        var text = line.Length == 0 ? string.Empty : line[1..];
        if (line.Length == 0)
        {
            warnings.Add($"Treated an empty line inside a hunk of '{headerOldPath ?? headerNewPath}' as an empty context line");
        }

        switch (prefix)
        {
            case ' ':
                openLines.Add(new DiffLine(DiffLineKind.Context, text, nextOldNumber++, nextNewNumber++, false));
                remainingOld--;
                remainingNew--;
                break;
            case '-':
                openLines.Add(new DiffLine(DiffLineKind.Removed, text, nextOldNumber++, null, false));
                remainingOld--;
                break;
            case '+':
                openLines.Add(new DiffLine(DiffLineKind.Added, text, null, nextNewNumber++, false));
                remainingNew--;
                break;
            default:
                warnings.Add($"Hunk of '{headerOldPath ?? headerNewPath}' ended early at an unreadable line: '{line}'");
                remainingOld = 0;
                remainingNew = 0;
                break;
        }
    }

    private void CloseHunk()
    {
        if (openHeaderText is null)
        {
            return;
        }

        if (remainingOld > 0 || remainingNew > 0)
        {
            warnings.Add($"Hunk '{openHeaderText}' of '{headerOldPath ?? headerNewPath}' ended before its line counts were reached");
        }

        hunks.Add(new DiffHunk(openHeaderText, openOldStart, openOldCount, openNewStart, openNewCount, openSection, openLines.ToArray()));
        openLines.Clear();
        openHeaderText = null;
        remainingOld = 0;
        remainingNew = 0;
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
