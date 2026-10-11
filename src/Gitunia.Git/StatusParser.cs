using System.Globalization;

namespace Gitunia.Git;

/// <summary>Parses <c>git status --porcelain=v2 -z --branch</c>: NUL-terminated records; a rename or copy is followed by its source path as an extra NUL-terminated field.</summary>
public static class StatusParser
{
    public static StatusSnapshot Parse(string output)
    {
        var entries = new List<StatusEntry>();
        var warnings = new List<string>();
        var header = new StatusHeaderBuilder();
        var segments = output.Split('\0');

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (segment.Length == 0)
            {
                continue;
            }

            if (segment.StartsWith("# ", StringComparison.Ordinal))
            {
                header.Add(segment, warnings);
                continue;
            }

            StatusEntry? entry;
            switch (segment[0])
            {
                case '1':
                    entry = ParseOrdinary(segment, warnings);
                    break;
                case '2':
                    // The source path belongs to this record even when the record itself is malformed.
                    entry = ParseRenamedOrCopied(segment, index + 1 < segments.Length ? segments[index + 1] : null, warnings);
                    index++;
                    break;
                case 'u':
                    entry = ParseUnmerged(segment, warnings);
                    break;
                case '?':
                    entry = ParsePathOnly(segment, StatusEntryKind.Untracked, warnings);
                    break;
                case '!':
                    entry = ParsePathOnly(segment, StatusEntryKind.Ignored, warnings);
                    break;
                default:
                    warnings.Add($"Skipped unknown status record '{segment}'");
                    entry = null;
                    break;
            }

            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return new StatusSnapshot(header.Build(), entries, warnings);
    }

    private static StatusEntry? ParseOrdinary(string record, List<string> warnings)
    {
        // 1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>
        var fields = record.Split(' ', 9);
        if (fields.Length < 9 || !TryParseCodes(fields[1], warnings, out var indexChange, out var worktreeChange))
        {
            return SkipMalformed(record, "ordinary", warnings);
        }

        return new StatusEntry(StatusEntryKind.Ordinary, fields[8], null, indexChange, worktreeChange, null, ConflictKind.None, ParseSubmodule(fields[2]));
    }

    private static StatusEntry? ParseRenamedOrCopied(string record, string? originalPath, List<string> warnings)
    {
        // 2 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <X><score> <path> NUL <origPath>
        var fields = record.Split(' ', 10);
        if (fields.Length < 10
            || string.IsNullOrEmpty(originalPath)
            || !TryParseCodes(fields[1], warnings, out var indexChange, out var worktreeChange)
            || !TryParseScore(fields[8], out var kind, out var similarity))
        {
            return SkipMalformed(record, "rename or copy", warnings);
        }

        return new StatusEntry(kind, fields[9], originalPath, indexChange, worktreeChange, similarity, ConflictKind.None, ParseSubmodule(fields[2]));
    }

    private static StatusEntry? ParseUnmerged(string record, List<string> warnings)
    {
        // u <XY> <sub> <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path>
        var fields = record.Split(' ', 11);
        if (fields.Length < 11 || !TryParseCodes(fields[1], warnings, out var indexChange, out var worktreeChange))
        {
            return SkipMalformed(record, "unmerged", warnings);
        }

        var conflict = fields[1] switch
        {
            "DD" => ConflictKind.BothDeleted,
            "AU" => ConflictKind.AddedByUs,
            "UD" => ConflictKind.DeletedByThem,
            "UA" => ConflictKind.AddedByThem,
            "DU" => ConflictKind.DeletedByUs,
            "AA" => ConflictKind.BothAdded,
            "UU" => ConflictKind.BothModified,
            _ => ConflictKind.None,
        };
        if (conflict == ConflictKind.None)
        {
            warnings.Add($"Unrecognised conflict code '{fields[1]}'");
        }

        return new StatusEntry(StatusEntryKind.Unmerged, fields[10], null, indexChange, worktreeChange, null, conflict, ParseSubmodule(fields[2]));
    }

    private static StatusEntry? ParsePathOnly(string record, StatusEntryKind kind, List<string> warnings)
    {
        // ? <path> or ! <path>
        if (record.Length < 3 || record[1] != ' ')
        {
            return SkipMalformed(record, kind.ToString().ToLowerInvariant(), warnings);
        }

        return new StatusEntry(kind, record[2..], null, FileChangeKind.Unmodified, FileChangeKind.Unmodified, null, ConflictKind.None, null);
    }

    private static StatusEntry? SkipMalformed(string record, string description, List<string> warnings)
    {
        warnings.Add($"Skipped malformed {description} status record '{record}'");
        return null;
    }

    private static bool TryParseCodes(string codes, List<string> warnings, out FileChangeKind indexChange, out FileChangeKind worktreeChange)
    {
        indexChange = default;
        worktreeChange = default;
        if (codes.Length != 2 || !TryParseCode(codes[0], out indexChange) || !TryParseCode(codes[1], out worktreeChange))
        {
            warnings.Add($"Unrecognised status code '{codes}'");
            return false;
        }

        return true;
    }

    private static bool TryParseCode(char code, out FileChangeKind kind)
    {
        (var known, kind) = code switch
        {
            '.' => (true, FileChangeKind.Unmodified),
            'M' => (true, FileChangeKind.Modified),
            'T' => (true, FileChangeKind.TypeChanged),
            'A' => (true, FileChangeKind.Added),
            'D' => (true, FileChangeKind.Deleted),
            'R' => (true, FileChangeKind.Renamed),
            'C' => (true, FileChangeKind.Copied),
            'U' => (true, FileChangeKind.UpdatedButUnmerged),
            _ => (false, FileChangeKind.Unmodified),
        };
        return known;
    }

    /// <summary>Reads <c>R100</c> or <c>C75</c>.</summary>
    private static bool TryParseScore(string score, out StatusEntryKind kind, out int similarity)
    {
        similarity = 0;
        kind = StatusEntryKind.Ordinary;
        if (score.Length < 2)
        {
            return false;
        }

        kind = score[0] switch
        {
            'R' => StatusEntryKind.Renamed,
            'C' => StatusEntryKind.Copied,
            _ => StatusEntryKind.Ordinary,
        };
        return kind != StatusEntryKind.Ordinary
            && int.TryParse(score.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out similarity);
    }

    /// <summary>Reads <c>N...</c> (not a submodule) or <c>S&lt;c&gt;&lt;m&gt;&lt;u&gt;</c>.</summary>
    private static SubmoduleState? ParseSubmodule(string field) =>
        field.Length == 4 && field[0] == 'S'
            ? new SubmoduleState(field[1] == 'C', field[2] == 'M', field[3] == 'U')
            : null;
}
