using System.Text;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Git.Commits;

/// <summary>
/// Incremental parser for <c>git log</c> output in <see cref="CommitLogFormat"/>. Feed it chunks of any size;
/// it keeps only the bytes of the commit that is still incomplete, so memory does not grow with the log.
/// </summary>
public sealed class CommitLogParser
{
    private static readonly ObjectId[] NoParents = [];
    private static readonly CommitDecoration[] NoDecorations = [];

    private readonly List<byte> carry = [];
    private readonly List<string> warnings = [];
    private readonly Dictionary<string, string> internedText = [];

    /// <summary>Everything the parser could not understand so far.</summary>
    public IReadOnlyList<string> Warnings => warnings;

    /// <summary>Bytes of an unfinished commit held back (never more than one commit).</summary>
    public int BufferedByteCount => carry.Count;

    /// <summary>Parses the next chunk and appends every commit completed by it to <paramref name="output"/>.</summary>
    public void Feed(ReadOnlySpan<byte> chunk, ICollection<CommitInfo> output)
    {
        while (chunk.Length > 0)
        {
            var terminator = chunk.IndexOf(GitSeparators.RecordTerminator);
            if (terminator < 0)
            {
                foreach (var value in chunk)
                {
                    carry.Add(value);
                }

                return;
            }

            if (carry.Count == 0)
            {
                ParseRecord(chunk[..terminator], output);
            }
            else
            {
                foreach (var value in chunk[..terminator])
                {
                    carry.Add(value);
                }

                ParseRecord(carry.ToArray(), output);
                carry.Clear();
            }

            chunk = chunk[(terminator + 1)..];
        }
    }

    /// <summary>Call after the last chunk; reports a truncated final commit.</summary>
    public void Complete()
    {
        if (carry.Count > 0)
        {
            warnings.Add($"Output ended inside a commit record ({carry.Count} bytes dropped)");
            carry.Clear();
        }
    }

    private static void SplitFields(ReadOnlySpan<byte> record, Span<Range> fields, out int count)
    {
        count = 0;
        var start = 0;
        for (var index = 0; index < record.Length && count < fields.Length - 1; index++)
        {
            if (record[index] == GitSeparators.UnitSeparator)
            {
                fields[count++] = start..index;
                start = index + 1;
            }
        }

        fields[count++] = start..record.Length;
    }

    private static IReadOnlyList<CommitDecoration> ParseDecorations(string text)
    {
        if (text.Length == 0)
        {
            return NoDecorations;
        }

        var decorations = new List<CommitDecoration>();
        foreach (var part in text.Split(", "))
        {
            var name = part;
            var current = false;
            if (name.StartsWith("HEAD -> ", StringComparison.Ordinal))
            {
                name = name["HEAD -> ".Length..];
                current = true;
            }
            else if (name == "HEAD")
            {
                decorations.Add(new CommitDecoration("HEAD", "HEAD", RefKind.Head, true));
                continue;
            }

            var isTag = name.StartsWith("tag: ", StringComparison.Ordinal);
            if (isTag)
            {
                name = name["tag: ".Length..];
            }

            decorations.Add(Decorate(name, isTag, current));
        }

        return decorations;
    }

    private static CommitDecoration Decorate(string fullName, bool isTag, bool current)
    {
        (var kind, var prefix) = fullName switch
        {
            _ when isTag => (RefKind.Tag, GitRefPrefixes.Tags),
            _ when fullName.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal) => (RefKind.LocalBranch, GitRefPrefixes.Heads),
            _ when fullName.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal) => (RefKind.RemoteBranch, GitRefPrefixes.Remotes),
            _ => (RefKind.Other, string.Empty),
        };
        var shortName = fullName.StartsWith(prefix, StringComparison.Ordinal) ? fullName[prefix.Length..] : fullName;
        return new CommitDecoration(fullName, shortName, kind, current);
    }

    private void ParseRecord(ReadOnlySpan<byte> record, ICollection<CommitInfo> output)
    {
        // A leading newline can appear when git separates records with both a NUL and a line feed.
        if (record.Length > 0 && record[0] == (byte)'\n')
        {
            record = record[1..];
        }

        if (record.Length == 0)
        {
            return;
        }

        Span<Range> fields = stackalloc Range[CommitLogFormat.FieldCount];
        SplitFields(record, fields, out var count);
        if (count < CommitLogFormat.FieldCount)
        {
            warnings.Add($"Skipped commit record with {count} fields");
            return;
        }

        if (!ObjectId.TryParse(record[fields[0]], out var id))
        {
            warnings.Add("Skipped commit record with an invalid hash");
            return;
        }

        var parents = ParseParents(record[fields[1]]);
        if (parents is null)
        {
            warnings.Add($"Skipped commit {id} with an invalid parent list");
            return;
        }

        if (!TryParseAuthorTime(record[fields[4]], out var authorTime))
        {
            warnings.Add($"Commit {id} has an invalid author time");
        }

        output.Add(new CommitInfo(
            id,
            parents,
            Intern(record[fields[2]]),
            Intern(record[fields[3]]),
            authorTime,
            Encoding.UTF8.GetString(record[fields[6]]),
            ParseDecorations(Encoding.UTF8.GetString(record[fields[5]]))));
    }

    // Raw date: "<unix seconds> <+|-HHMM>"; a bare number counts as UTC.
    private static bool TryParseAuthorTime(ReadOnlySpan<byte> field, out DateTimeOffset result)
    {
        var space = field.IndexOf((byte)' ');
        var secondsField = space < 0 ? field : field[..space];
        if (!long.TryParse(secondsField, out var unixSeconds) || unixSeconds < -62_135_596_800L || unixSeconds > 253_402_300_799L)
        {
            result = DateTimeOffset.UnixEpoch;
            return false;
        }

        var offset = TimeSpan.Zero;
        if (space >= 0 && field.Length - space == 6 && (field[space + 1] == (byte)'+' || field[space + 1] == (byte)'-'))
        {
            var digits = field[(space + 2)..];
            if (IsDigits(digits) && int.TryParse(digits[..2], out var hours) && int.TryParse(digits[2..], out var minutes) && hours <= 14 && minutes < 60)
            {
                offset = new TimeSpan(hours, minutes, 0);
                if (field[space + 1] == (byte)'-')
                {
                    offset = -offset;
                }
            }
        }

        var instant = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        try
        {
            result = instant.ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Local time would leave years 1..9999 (a forged date at the edge): keep the UTC instant.
            result = instant;
        }

        return true;
    }

    private static bool IsDigits(ReadOnlySpan<byte> text)
    {
        foreach (var value in text)
        {
            if (value < (byte)'0' || value > (byte)'9')
            {
                return false;
            }
        }

        return true;
    }

    private static ObjectId[]? ParseParents(ReadOnlySpan<byte> field)
    {
        if (field.Length == 0)
        {
            return NoParents;
        }

        var parents = new List<ObjectId>(2);
        while (field.Length > 0)
        {
            var space = field.IndexOf((byte)' ');
            var hash = space < 0 ? field : field[..space];
            if (!ObjectId.TryParse(hash, out var parent))
            {
                return null;
            }

            parents.Add(parent);
            field = space < 0 ? [] : field[(space + 1)..];
        }

        return parents.ToArray();
    }

    /// <summary>Author names and emails repeat across thousands of commits; share one string per distinct value.</summary>
    private string Intern(ReadOnlySpan<byte> utf8)
    {
        var text = Encoding.UTF8.GetString(utf8);
        if (internedText.TryGetValue(text, out var existing))
        {
            return existing;
        }

        internedText[text] = text;
        return text;
    }
}
