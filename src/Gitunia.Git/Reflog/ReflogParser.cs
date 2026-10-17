using Gitunia.Git.Reflog.Models;

namespace Gitunia.Git.Reflog;

/// <summary>
/// Parses the output of <c>git reflog show</c> produced with <see cref="Format"/> and <see cref="DateOption"/>.
/// </summary>
public static class ReflogParser
{
    /// <summary>
    /// The <c>--date</c> value that makes the selector carry the relative time (<c>HEAD@{3 hours ago}</c>).
    /// </summary>
    public const string DateOption = "--date=relative";

    /// <summary>
    /// The <c>--format</c> value the parser expects: hash, selector and reflog subject, each entry ended by a
    /// record separator.
    /// </summary>
    public const string Format =
        $"--format=%H{Unit}%gd{Unit}%gs{GitSeparators.LogRecordSeparatorPlaceholder}";

    /// <summary>Number of fields in <see cref="Format"/>.</summary>
    public const int FieldCount = 3;

    private const string Unit = GitSeparators.UnitSeparatorPlaceholder;
    private const string SelectorOpen = "@{";
    private const string SelectorClose = "}";
    private const string ActionSeparator = ": ";

    public static ParseResult<ReflogEntry> Parse(string output)
    {
        var entries = new List<ReflogEntry>();
        var warnings = new List<string>();
        foreach (var record in output.Split(GitSeparators.RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = record.Trim('\n');
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split((char)GitSeparators.UnitSeparator);
            if (fields.Length < FieldCount)
            {
                warnings.Add($"Skipped reflog line with {fields.Length} fields: '{line}'");
                continue;
            }

            // The reflog message is "action: detail"; a message without a colon is all action.
            var subject = fields[2];
            var separator = subject.IndexOf(ActionSeparator, StringComparison.Ordinal);
            var action = separator < 0 ? subject : subject[..separator];
            var message = separator < 0 ? string.Empty : subject[(separator + ActionSeparator.Length)..];
            entries.Add(new ReflogEntry(entries.Count, fields[0], action, message, DateOf(fields[1])));
        }

        return new ParseResult<ReflogEntry>(entries, warnings);
    }

    private static string DateOf(string selector)
    {
        var open = selector.IndexOf(SelectorOpen, StringComparison.Ordinal);
        if (open < 0 || !selector.EndsWith(SelectorClose, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var start = open + SelectorOpen.Length;
        return selector[start..^SelectorClose.Length];
    }
}
