using System.Globalization;
using System.Text;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Git.Commits;

/// <summary>Parses the output of <c>git log -1 -z</c> in <see cref="CommitDetailsFormat"/> into a <see cref="CommitDetails"/> without files.</summary>
public static class CommitDetailsParser
{
    private const int FieldCount = 9;

    private static readonly ObjectId[] NoParents = [];

    /// <param name="output">Raw stdout.</param>
    /// <param name="warnings">Receives a message when something could not be understood.</param>
    /// <returns>The commit, or null when the output holds no usable record.</returns>
    public static CommitDetails? Parse(ReadOnlySpan<byte> output, ICollection<string> warnings)
    {
        var terminator = output.IndexOf((byte)0);
        var record = terminator < 0 ? output : output[..terminator];
        if (record.Length > 0 && record[0] == (byte)'\n')
        {
            record = record[1..];
        }

        if (record.Length == 0)
        {
            warnings.Add("Commit output was empty");
            return null;
        }

        Span<Range> fields = stackalloc Range[FieldCount];
        var count = 0;
        var start = 0;
        for (var index = 0; index < record.Length && count < FieldCount - 1; index++)
        {
            if (record[index] == CommitDetailsFormat.FieldSeparator)
            {
                fields[count++] = start..index;
                start = index + 1;
            }
        }

        fields[count++] = start..record.Length;
        if (count < FieldCount)
        {
            warnings.Add($"Commit record has {count} fields instead of {FieldCount}");
            return null;
        }

        if (!ObjectId.TryParse(record[fields[0]], out var id))
        {
            warnings.Add("Commit record has an invalid hash");
            return null;
        }

        var parents = ParseParents(record[fields[1]]);
        if (parents is null)
        {
            warnings.Add($"Commit {id} has an invalid parent list");
            return null;
        }

        return new CommitDetails(
            id,
            parents,
            Encoding.UTF8.GetString(record[fields[2]]),
            Encoding.UTF8.GetString(record[fields[3]]),
            ParseTime(record[fields[4]], id, warnings),
            Encoding.UTF8.GetString(record[fields[5]]),
            Encoding.UTF8.GetString(record[fields[6]]),
            ParseTime(record[fields[7]], id, warnings),
            Encoding.UTF8.GetString(record[fields[8]]).TrimEnd('\n', '\r'),
            []);
    }

    private static DateTimeOffset ParseTime(ReadOnlySpan<byte> field, ObjectId id, ICollection<string> warnings)
    {
        if (DateTimeOffset.TryParse(Encoding.UTF8.GetString(field), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        warnings.Add($"Commit {id} has an invalid time");
        return DateTimeOffset.UnixEpoch;
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
}
