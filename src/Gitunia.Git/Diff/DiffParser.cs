using System.Text;
using System.Text.Unicode;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Git.Diff;

/// <summary>
/// Incremental parser for the patch text of <c>git diff</c>, <c>git show</c> and <c>git log -p</c> (with
/// <c>--no-color</c>). Feed it chunks of any size; a file is emitted as soon as the next one starts, so memory is
/// bounded by the largest single file, not by the diff. Anything before the first <c>diff --git</c> line (such as the
/// commit message of <c>git show</c>) is ignored. Combined diffs (<c>diff --cc</c>, merge commits) are skipped with a warning.
/// </summary>
public sealed class DiffParser(int maximumLinesPerFile = int.MaxValue)
{
    private const string FileHeaderPrefix = "diff --git ";

    private readonly List<byte> _carry = [];
    private readonly List<string> _warnings = [];

    private DiffFileBuilder? _current;
    private bool _skippingCombined;
    private bool _reportedInvalidText;

    /// <summary>Everything the parser could not understand so far.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Bytes of an unfinished line held back (never more than one line).</summary>
    public int BufferedByteCount => _carry.Count;

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
                    _carry.Add(value);
                }

                return;
            }

            if (_carry.Count == 0)
            {
                AddLine(chunk[..terminator], output);
            }
            else
            {
                foreach (var value in chunk[..terminator])
                {
                    _carry.Add(value);
                }

                AddLine(_carry.ToArray(), output);
                _carry.Clear();
            }

            chunk = chunk[(terminator + 1)..];
        }
    }

    /// <summary>Call after the last chunk; emits the final file.</summary>
    public void Complete(ICollection<DiffFile> output)
    {
        if (_carry.Count > 0)
        {
            AddLine(_carry.ToArray(), output);
            _carry.Clear();
        }

        EmitCurrent(output);
    }

    private void AddLine(ReadOnlySpan<byte> bytes, ICollection<DiffFile> output)
    {
        var line = Decode(bytes);
        var startsFile = line.StartsWith(FileHeaderPrefix, StringComparison.Ordinal);
        var startsCombined = line.StartsWith("diff --cc ", StringComparison.Ordinal) || line.StartsWith("diff --combined ", StringComparison.Ordinal);
        if (_current is not null && _current.IsInHunkBody)
        {
            _current.Add(line);
            return;
        }

        if (startsFile || startsCombined)
        {
            EmitCurrent(output);
            _skippingCombined = startsCombined;
            if (startsCombined)
            {
                _warnings.Add($"Skipped combined diff '{line}'");
            }
            else
            {
                _current = new DiffFileBuilder(line, _warnings, maximumLinesPerFile);
            }

            return;
        }

        if (!_skippingCombined)
        {
            _current?.Add(line);
        }
    }

    private void EmitCurrent(ICollection<DiffFile> output)
    {
        if (_current is not null)
        {
            output.Add(_current.Build());
            _current = null;
        }
    }

    /// <summary>Decodes UTF-8; invalid bytes become U+FFFD (once per parser a warning says the text is not an exact copy).</summary>
    private string Decode(ReadOnlySpan<byte> bytes)
    {
        if (!_reportedInvalidText && !Utf8.IsValid(bytes))
        {
            _reportedInvalidText = true;
            _warnings.Add("The diff contains bytes that are not valid UTF-8; they were replaced and the text is not an exact copy");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
