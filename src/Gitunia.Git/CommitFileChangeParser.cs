using System.Globalization;
using System.Text;

namespace Gitunia.Git;

/// <summary>Parses <c>git diff-tree --raw -z</c> output into the files a commit changed.</summary>
public static class CommitFileChangeParser
{
    public static ParseResult<CommitFileChange> Parse(ReadOnlySpan<byte> output)
    {
        var files = new List<CommitFileChange>();
        var warnings = new List<string>();
        var position = 0;
        while (position < output.Length)
        {
            var metadata = NextField(output, ref position);
            if (metadata.Length == 0)
            {
                continue;
            }

            // ":100644 100644 <old> <new> M", with a score right after the letter for renames and copies.
            var space = metadata.LastIndexOf((byte)' ');
            if (metadata[0] != (byte)':' || space < 0 || space + 1 >= metadata.Length)
            {
                warnings.Add("Skipped a malformed raw diff entry");
                continue;
            }

            var statusText = Encoding.UTF8.GetString(metadata[(space + 1)..]);
            var hasTwoPaths = statusText[0] is 'R' or 'C';
            var first = NextField(output, ref position);
            var second = hasTwoPaths ? NextField(output, ref position) : default;
            if (first.Length == 0 || (hasTwoPaths && second.Length == 0))
            {
                warnings.Add("Skipped a raw diff entry without a path");
                continue;
            }

            if (KindOf(statusText[0]) is not { } kind)
            {
                warnings.Add($"Skipped a raw diff entry with the unknown status '{statusText}'");
                continue;
            }

            var firstPath = Encoding.UTF8.GetString(first);
            files.Add(hasTwoPaths
                ? new CommitFileChange(Encoding.UTF8.GetString(second), firstPath, kind, ParseSimilarity(statusText))
                : new CommitFileChange(firstPath, null, kind, null));
        }

        return new ParseResult<CommitFileChange>(files, warnings);
    }

    private static ReadOnlySpan<byte> NextField(ReadOnlySpan<byte> output, ref int position)
    {
        if (position >= output.Length)
        {
            return default;
        }

        var rest = output[position..];
        var end = rest.IndexOf((byte)0);
        var field = end < 0 ? rest : rest[..end];
        position += end < 0 ? rest.Length : end + 1;
        return field;
    }

    private static FileChangeKind? KindOf(char letter) => letter switch
    {
        'A' => FileChangeKind.Added,
        'M' => FileChangeKind.Modified,
        'D' => FileChangeKind.Deleted,
        'R' => FileChangeKind.Renamed,
        'C' => FileChangeKind.Copied,
        'T' => FileChangeKind.TypeChanged,
        'U' => FileChangeKind.UpdatedButUnmerged,
        _ => null,
    };

    private static int? ParseSimilarity(string statusText) =>
        statusText.Length > 1 && int.TryParse(statusText.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var score) ? score : null;
}
