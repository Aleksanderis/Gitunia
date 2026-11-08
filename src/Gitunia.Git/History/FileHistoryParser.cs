using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.History.Models;

namespace Gitunia.Git.History;

/// <summary>
/// Parses the output of <c>git log --follow --name-status -z</c> produced with <see cref="Format"/>: one record per
/// commit, started by a record separator, then the header fields, then the status letter and the path (two paths for a
/// rename or copy), all NUL-separated.
/// </summary>
public static class FileHistoryParser
{
    /// <summary>The <c>--format</c> value the parser expects: hash, author name, short date and subject.</summary>
    public const string Format =
        $"--format={GitSeparators.LogRecordSeparatorPlaceholder}%H{Unit}%an{Unit}%ad{Unit}%s";

    /// <summary>The <c>--date</c> value that makes the date field short.</summary>
    public const string DateOption = "--date=short";

    /// <summary>Number of header fields in <see cref="Format"/>.</summary>
    public const int FieldCount = 4;

    private const string Unit = GitSeparators.UnitSeparatorPlaceholder;
    private const int HashField = 0;
    private const int AuthorField = 1;
    private const int DateField = 2;
    private const int SubjectField = 3;
    private const int StatusToken = 1;
    private const int FirstPathToken = 2;
    private const int SecondPathToken = 3;

    /// <summary>Parses the output.</summary>
    /// <param name="output">The git output.</param>
    /// <param name="followedPath">The path the log was asked for, used for a commit that prints no status line.</param>
    public static ParseResult<FileHistoryEntry> Parse(string output, string followedPath)
    {
        var entries = new List<FileHistoryEntry>();
        var warnings = new List<string>();
        var path = followedPath;
        foreach (var record in output.Split(GitSeparators.RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = record.Split((char)GitSeparators.RecordTerminator);
            var fields = tokens[0].Split((char)GitSeparators.UnitSeparator);
            if (fields.Length < FieldCount)
            {
                warnings.Add($"Skipped file history record with {fields.Length} fields: '{tokens[0]}'");
                continue;
            }

            var entry = EntryOf(fields, tokens, path);
            entries.Add(entry);

            // Older commits are followed under the name the file had before this one.
            path = entry.OldPath ?? entry.Path;
        }

        return new ParseResult<FileHistoryEntry>(entries, warnings);
    }

    private static FileHistoryEntry EntryOf(string[] fields, string[] tokens, string fallbackPath)
    {
        var kind = FileChangeKind.Modified;
        var path = fallbackPath;
        string? oldPath = null;
        var status = tokens.Length > StatusToken ? tokens[StatusToken].Trim('\n') : string.Empty;
        if (status.Length > 0 && FileChangeLetters.KindOf(status[0], false) is { } letterKind)
        {
            kind = letterKind;
            var isTwoPaths = kind is FileChangeKind.Renamed or FileChangeKind.Copied;
            if (isTwoPaths && tokens.Length > SecondPathToken)
            {
                oldPath = tokens[FirstPathToken];
                path = tokens[SecondPathToken];
            }
            else if (tokens.Length > FirstPathToken)
            {
                path = tokens[FirstPathToken];
            }
        }

        return new FileHistoryEntry(
            fields[HashField].Trim('\n'),
            fields[AuthorField],
            fields[DateField],
            fields[SubjectField],
            path,
            oldPath,
            kind);
    }
}
