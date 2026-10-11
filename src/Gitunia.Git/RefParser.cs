using System.Globalization;

namespace Gitunia.Git;

/// <summary>Parses the output of <c>for-each-ref</c> produced with <see cref="Format"/>.</summary>
public static class RefParser
{
    /// <summary>The <c>--format</c> value the parser expects: eight NUL-separated fields, one ref per line.</summary>
    public const string Format =
        "%(refname)%00%(objectname)%00%(*objectname)%00%(upstream)%00%(upstream:track,nobracket)%00%(HEAD)%00%(worktreepath)%00%(symref)%0a";

    private const int FieldCount = 8;

    public static ParseResult<GitRef> Parse(string output)
    {
        var refs = new List<GitRef>();
        var warnings = new List<string>();
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.Split('\0');
            if (fields.Length < FieldCount)
            {
                warnings.Add($"Skipped ref line with {fields.Length} fields: '{line}'");
                continue;
            }

            string fullName = fields[0];
            RefKind kind = KindOf(fullName);
            if (kind == RefKind.Other)
            {
                warnings.Add($"Skipped ref outside heads, remotes and tags: '{fullName}'");
                continue;
            }

            (int? ahead, int? behind, bool gone) = ParseTrack(fields[4], hasUpstream: fields[3].Length > 0, warnings);
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
        int second = fullName.IndexOf('/', "refs/".Length);
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

        int ahead = 0;
        int behind = 0;
        foreach (string part in track.Split(',', StringSplitOptions.TrimEntries))
        {
            string[] words = part.Split(' ');
            if (words.Length == 2 && int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count))
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
