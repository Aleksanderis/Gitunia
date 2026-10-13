using System.Globalization;

namespace Gitunia.Git;

/// <summary>Parses the output of <c>for-each-ref</c> produced with <see cref="Format"/>.</summary>
public static class RefParser
{
    /// <summary>The <c>--format</c> value the parser expects: eight NUL-separated fields, each ref ended by a record separator (0x1e) so paths with newlines survive.</summary>
    public const string Format =
        "%(refname)%00%(objectname)%00%(*objectname)%00%(upstream)%00%(upstream:track,nobracket)%00%(HEAD)%00%(worktreepath)%00%(symref)%1e";

    private const int FieldCount = 8;

    public static ParseResult<GitRef> Parse(string output)
    {
        var refs = new List<GitRef>();
        var warnings = new List<string>();
        foreach (var record in output.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = record.Trim('\n');
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split('\0');
            if (fields.Length < FieldCount)
            {
                warnings.Add($"Skipped ref line with {fields.Length} fields: '{line}'");
                continue;
            }

            var fullName = fields[0];
            var kind = KindOf(fullName);
            if (kind == RefKind.Other)
            {
                warnings.Add($"Skipped ref outside heads, remotes and tags: '{fullName}'");
                continue;
            }

            (var ahead, var behind, var gone) = ParseTrack(fields[4], hasUpstream: fields[3].Length > 0, warnings);
            refs.Add(new GitRef(
                fullName,
                ShortName(fullName),
                kind,
                fields[1],
                fields[2].Length > 0 ? fields[2] : fields[1],
                NullIfEmpty(fields[3]),
                ahead,
                behind,
                gone,
                fields[5] == "*",
                NullIfEmpty(fields[6]),
                NullIfEmpty(fields[7])));
        }

        return new ParseResult<GitRef>(refs, warnings);
    }

    private static RefKind KindOf(string fullName) => fullName switch
    {
        _ when fullName.StartsWith("refs/heads/", StringComparison.Ordinal) => RefKind.LocalBranch,
        _ when fullName.StartsWith("refs/remotes/", StringComparison.Ordinal) => RefKind.RemoteBranch,
        _ when fullName.StartsWith("refs/tags/", StringComparison.Ordinal) => RefKind.Tag,
        _ => RefKind.Other,
    };

    private static string ShortName(string fullName)
    {
        var second = fullName.IndexOf('/', "refs/".Length);
        return fullName[(second + 1)..];
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>Reads <c>ahead 2, behind 1</c>, <c>ahead 2</c>, <c>behind 1</c>, <c>gone</c> or an empty string (in sync or no upstream).</summary>
    private static (int? Ahead, int? Behind, bool Gone) ParseTrack(string track, bool hasUpstream, List<string> warnings)
    {
        if (track.Length == 0)
        {
            return hasUpstream ? (0, 0, false) : (null, null, false);
        }

        if (track == "gone")
        {
            return (null, null, true);
        }

        var ahead = 0;
        var behind = 0;
        foreach (var part in track.Split(',', StringSplitOptions.TrimEntries))
        {
            var words = part.Split(' ');
            if (words.Length == 2 && int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                if (words[0] == "ahead")
                {
                    ahead = count;
                    continue;
                }

                if (words[0] == "behind")
                {
                    behind = count;
                    continue;
                }
            }

            warnings.Add($"Unrecognised tracking text '{track}'");
            return (null, null, false);
        }

        return (ahead, behind, false);
    }
}
