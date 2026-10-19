using System.Globalization;
using System.Text;
using Gitunia.Git.Execution;
using Gitunia.Git.Trees.Models;

namespace Gitunia.Git.Trees;

/// <summary>
/// Parses <c>git ls-tree -r -z --long</c> output: <c>mode SP type SP object SP size TAB path NUL</c>, the size
/// padded with spaces and <c>-</c> for a submodule.
/// </summary>
public static class CommitTreeParser
{
    private const int FieldCount = 4;
    private const string CommitType = "commit";
    private const string BlobType = "blob";

    public static ParseResult<TreeEntry> Parse(ReadOnlySpan<byte> output)
    {
        var entries = new List<TreeEntry>();
        var warnings = new List<string>();
        var position = 0;
        while (position < output.Length)
        {
            var rest = output[position..];
            var end = rest.IndexOf((byte)0);
            var record = end < 0 ? rest : rest[..end];
            position += end < 0 ? rest.Length : end + 1;
            if (record.Length == 0)
            {
                continue;
            }

            if (ParseRecord(record) is { } entry)
            {
                entries.Add(entry);
            }
            else
            {
                warnings.Add("Skipped a malformed tree entry");
            }
        }

        return new ParseResult<TreeEntry>(entries, warnings);
    }

    private static TreeEntry? ParseRecord(ReadOnlySpan<byte> record)
    {
        var tab = record.IndexOf(CommitTreeFormat.PathSeparator);
        if (tab <= 0 || tab + 1 >= record.Length)
        {
            return null;
        }

        var fields = Encoding.ASCII
            .GetString(record[..tab])
            .Split(' ', FieldCount, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != FieldCount || !ObjectId.TryParse(fields[2], out var id))
        {
            return null;
        }

        var isSubmodule = fields[1] == CommitType;
        if (!isSubmodule && fields[1] != BlobType)
        {
            return null;
        }

        long? size = null;
        if (!isSubmodule && fields[3] != CommitTreeFormat.NoSize)
        {
            if (!long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return null;
            }

            size = parsed;
        }

        var kind = fields[0] switch
        {
            CommitTreeFormat.SubmoduleMode => TreeEntryKind.Submodule,
            CommitTreeFormat.SymlinkMode => TreeEntryKind.Symlink,
            CommitTreeFormat.ExecutableMode => TreeEntryKind.Executable,
            _ => isSubmodule ? TreeEntryKind.Submodule : TreeEntryKind.File,
        };

        return new TreeEntry(GitPathText.Decode(record[(tab + 1)..]), kind, id, size);
    }
}
