using System.Globalization;

namespace Gitunia.Git;

/// <summary>Parses <c>git stash list</c> output produced with <see cref="Format"/>.</summary>
public static class StashParser
{
    /// <summary>The <c>--format</c> value the parser expects: selector, hash and subject separated by NUL, each entry ended by 0x1e.</summary>
    public const string Format = "%gd%x00%H%x00%gs%x1e";

    private const string Prefix = "stash@{";
    private const int FieldCount = 3;

    public static ParseResult<StashEntry> Parse(string output)
    {
        var entries = new List<StashEntry>();
        var warnings = new List<string>();
        foreach (var record in output.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = record.Trim('\n');
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split('\0');
            if (fields.Length < FieldCount || !TryParseIndex(fields[0], out var index))
            {
                warnings.Add($"Skipped stash line that is not '<selector> NUL <hash> NUL <subject>': '{line}'");
                continue;
            }

            entries.Add(new StashEntry(index, fields[0], fields[1], fields[2]));
        }

        return new ParseResult<StashEntry>(entries, warnings);
    }

    private static bool TryParseIndex(string selector, out int index)
    {
        index = 0;
        return selector.StartsWith(Prefix, StringComparison.Ordinal)
            && selector.EndsWith('}')
            && int.TryParse(selector.AsSpan(Prefix.Length, selector.Length - Prefix.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }
}
