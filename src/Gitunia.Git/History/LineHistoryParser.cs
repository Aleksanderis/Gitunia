using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff;
using Gitunia.Git.History.Models;

namespace Gitunia.Git.History;

/// <summary>
/// Parses the output of <c>git log -L&lt;start&gt;,&lt;end&gt;:&lt;path&gt;</c> produced with
/// <see cref="FileHistoryParser.Format"/>: one record per commit, started by a record separator, a header line, a blank
/// line and the commit's patch limited to the range. Each entry keeps its patch; the file's name in the commit is read
/// from the patch header, so a renamed file is followed under both names.
/// </summary>
public static class LineHistoryParser
{
    private const int HashField = 0;
    private const int AuthorField = 1;
    private const int DateField = 2;
    private const int SubjectField = 3;

    /// <summary>Parses the output.</summary>
    /// <param name="output">The git output.</param>
    /// <param name="followedPath">The path the log was asked for, used for a commit whose patch names no file.</param>
    public static ParseResult<FileHistoryEntry> Parse(string output, string followedPath)
    {
        var entries = new List<FileHistoryEntry>();
        var warnings = new List<string>();
        foreach (var record in output.Split(GitSeparators.RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var lineEnd = record.IndexOf('\n', StringComparison.Ordinal);
            var header = lineEnd < 0 ? record : record[..lineEnd];
            var patch = lineEnd < 0 ? string.Empty : record[(lineEnd + 1)..].Trim('\n');
            var fields = header.Split((char)GitSeparators.UnitSeparator);
            if (fields.Length < FileHistoryParser.FieldCount)
            {
                warnings.Add($"Skipped line history record with {fields.Length} fields: '{header}'");
                continue;
            }

            entries.Add(EntryOf(fields, patch, followedPath));
        }

        return new ParseResult<FileHistoryEntry>(entries, warnings);
    }

    private static FileHistoryEntry EntryOf(string[] fields, string patch, string fallbackPath)
    {
        var files = patch.Length == 0 ? [] : DiffParser.Parse(patch).Items;
        var file = files.Count > 0 ? files[0] : null;
        var path = file is { Path.Length: > 0 } ? file.Path : fallbackPath;
        var oldPath = file is { OldPath: { } old, NewPath: { } added } && old != added ? old : null;
        var kind = oldPath is null ? FileChangeKind.Modified : FileChangeKind.Renamed;

        return new FileHistoryEntry(
            fields[HashField].Trim('\n'),
            fields[AuthorField],
            fields[DateField],
            fields[SubjectField],
            path,
            oldPath,
            kind,
            patch);
    }
}
