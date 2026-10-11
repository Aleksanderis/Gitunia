using System.Text;
using System.Text.Unicode;

namespace Gitunia.Git;

/// <summary>
/// Incremental parser for the patch text of <c>git diff</c>, <c>git show</c> and <c>git log -p</c> (with
/// <c>--no-color</c>). Feed it chunks of any size; a file is emitted as soon as the next one starts, so memory is
/// bounded by the largest single file, not by the diff. Anything before the first <c>diff --git</c> line (such as the
/// commit message of <c>git show</c>) is ignored. Combined diffs (<c>diff --cc</c>, merge commits) are skipped with a warning.
/// </summary>
public sealed class DiffParser
{
    private const string FileHeaderPrefix = "diff --git ";

    private readonly List<byte> carry = [];
    private readonly List<string> warnings = [];

    private DiffFileBuilder? current;
    private bool skippingCombined;
    private bool reportedInvalidText;

    /// <summary>Everything the parser could not understand so far.</summary>
    public IReadOnlyList<string> Warnings => warnings;

    /// <summary>Bytes of an unfinished line held back (never more than one line).</summary>
    public int BufferedByteCount => carry.Count;

    /// <summary>Parses a whole diff at once.</summary>
    public static ParseResult<DiffFile> Parse(string patch)
    {
        var parser = new DiffParser();
        var files = new List<DiffFile>();
        parser.Feed(Encoding.UTF8.GetBytes(patch), files);
        parser.Complete(files);
        return new ParseResult<DiffFile>(files, parser.Warnings);
    }

    /// <summary>Parses the next chunk and appends every file completed by it to <paramref name="output"/>.</summary>
    public void Feed(ReadOnlySpan<byte> chunk, ICollection<DiffFile> output)
    {
        while (chunk.Length > 0)
        {
            var terminator = chunk.IndexOf((byte)'\n');
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
                AddLine(chunk[..terminator], output);
            }
            else
            {
                foreach (var value in chunk[..terminator])
                {
                    carry.Add(value);
                }

                AddLine(carry.ToArray(), output);
                carry.Clear();
            }

            chunk = chunk[(terminator + 1)..];
        }
    }

    /// <summary>Call after the last chunk; emits the final file.</summary>
    public void Complete(ICollection<DiffFile> output)
    {
        if (carry.Count > 0)
        {
            AddLine(carry.ToArray(), output);
            carry.Clear();
        }

        EmitCurrent(output);
    }

    private void AddLine(ReadOnlySpan<byte> bytes, ICollection<DiffFile> output)
    {
        var line = Decode(bytes);
        var startsFile = line.StartsWith(FileHeaderPrefix, StringComparison.Ordinal);
        var startsCombined = line.StartsWith("diff --cc ", StringComparison.Ordinal) || line.StartsWith("diff --combined ", StringComparison.Ordinal);
        if (current is not null && current.IsInHunkBody)
        {
            current.Add(line);
            return;
        }

        if (startsFile || startsCombined)
        {
            EmitCurrent(output);
            skippingCombined = startsCombined;
            if (startsCombined)
            {
                warnings.Add($"Skipped combined diff '{line}'");
            }
            else
            {
                current = new DiffFileBuilder(line, warnings);
            }

            return;
        }

        if (!skippingCombined)
        {
            current?.Add(line);
        }
    }

    private void EmitCurrent(ICollection<DiffFile> output)
    {
        if (current is not null)
        {
            output.Add(current.Build());
            current = null;
        }
    }

    /// <summary>Decodes UTF-8; invalid bytes become U+FFFD (once per parser a warning says the text is not an exact copy).</summary>
    private string Decode(ReadOnlySpan<byte> bytes)
    {
        if (!reportedInvalidText && !Utf8.IsValid(bytes))
        {
            reportedInvalidText = true;
            warnings.Add("The diff contains bytes that are not valid UTF-8; they were replaced and the text is not an exact copy");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
