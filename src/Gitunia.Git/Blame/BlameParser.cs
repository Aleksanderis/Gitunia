using System.Globalization;
using System.Text;
using Gitunia.Git.Blame.Models;

namespace Gitunia.Git.Blame;

/// <summary>
/// Parses the output of <c>git blame --incremental</c> as it streams in. Each chunk of output may end inside a line;
/// <see cref="Feed"/> keeps the rest. A record is a header line (<c>id originalLine finalLine count</c>), the commit's
/// headers (only the first time the commit appears) and a closing <c>filename</c> line. Unknown headers are ignored.
/// </summary>
public sealed class BlameParser
{
    private const byte LineFeed = (byte)'\n';
    private const char Space = ' ';
    private const int HeaderFieldCount = 4;
    private const int MinimumIdLength = 40;
    private const string AuthorKey = "author";
    private const string AuthorTimeKey = "author-time";
    private const string AuthorTimezoneKey = "author-tz";
    private const string SummaryKey = "summary";
    private const string BoundaryKey = "boundary";
    private const string FilenameKey = "filename";
    private const string PreviousKey = "previous";
    private const int MinutesPerHour = 60;
    private const int HundredMinutes = 100;

    private readonly List<byte> _pending = [];
    private readonly Dictionary<string, BlameCommit> _commits = [];
    private readonly List<string> _warnings = [];
    private Header? _header;
    private string _author = string.Empty;
    private string _summary = string.Empty;
    private long _authorTime;
    private TimeSpan _offset = TimeSpan.Zero;
    private bool _isBoundary;
    private string? _previousPath;

    /// <summary>What the parser could not understand, once per skipped line.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Takes the next bytes of output and adds the chunks they complete to <paramref name="completed"/>.</summary>
    /// <param name="bytes">The next piece of output.</param>
    /// <param name="completed">Receives the finished chunks, in the order git found them.</param>
    public void Feed(ReadOnlySpan<byte> bytes, List<BlameChunk> completed)
    {
        foreach (var value in bytes)
        {
            if (value != LineFeed)
            {
                _pending.Add(value);

                continue;
            }

            var line = Encoding.UTF8.GetString([.. _pending]);
            _pending.Clear();
            Line(line, completed);
        }
    }

    /// <summary>Ends the stream; a record cut short is reported as a warning.</summary>
    public void Complete()
    {
        if (_header is not null || _pending.Count > 0)
        {
            _warnings.Add("The blame output ended in the middle of a record");
        }
    }

    private void Line(string line, List<BlameChunk> completed)
    {
        if (_header is null)
        {
            StartRecord(line);

            return;
        }

        var separator = line.IndexOf(Space);
        var key = separator < 0 ? line : line[..separator];
        var value = separator < 0 ? string.Empty : line[(separator + 1)..];
        if (key == FilenameKey)
        {
            completed.Add(Finish(value));

            return;
        }

        Remember(key, value);
    }

    private void StartRecord(string line)
    {
        var fields = line.Split(Space);
        if (fields.Length == HeaderFieldCount
            && fields[0].Length >= MinimumIdLength
            && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var original)
            && int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var final)
            && int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            _header = new Header(fields[0], original, final, count);
            _author = string.Empty;
            _summary = string.Empty;
            _authorTime = 0;
            _offset = TimeSpan.Zero;
            _isBoundary = false;
            _previousPath = null;

            return;
        }

        if (line.Length > 0)
        {
            _warnings.Add($"Skipped a line of the blame output: {line}");
        }
    }

    private void Remember(string key, string value)
    {
        switch (key)
        {
            case AuthorKey:
                _author = value;
                break;
            case SummaryKey:
                _summary = value;
                break;
            case AuthorTimeKey:
                _ = long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _authorTime);
                break;
            case AuthorTimezoneKey:
                _offset = Offset(value);
                break;
            case PreviousKey:
                _previousPath = PathAfterId(value);
                break;
            case BoundaryKey:
                _isBoundary = true;
                break;
        }
    }

    // The value is "<parent id> <path>".
    private static string? PathAfterId(string value)
    {
        var separator = value.IndexOf(Space);

        return separator < 0 ? null : value[(separator + 1)..];
    }

    // A timezone is written +HHMM or -HHMM.
    private static TimeSpan Offset(string value)
    {
        if (value.Length < 2 || !int.TryParse(value.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var digits))
        {
            return TimeSpan.Zero;
        }

        var minutes = ((digits / HundredMinutes) * MinutesPerHour) + (digits % HundredMinutes);
        var offset = TimeSpan.FromMinutes(minutes);

        return value[0] == '-' ? -offset : offset;
    }

    private BlameChunk Finish(string path)
    {
        var header = _header!;
        _header = null;
        if (!_commits.TryGetValue(header.Id, out var commit))
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(_authorTime).ToOffset(_offset);
            commit = new BlameCommit(header.Id, _author, time, _summary, _isBoundary, path, _previousPath);
            _commits[header.Id] = commit;
        }

        return new BlameChunk(header.FinalLine - 1, header.Count, header.OriginalLine - 1, commit);
    }

    private sealed record Header(string Id, int OriginalLine, int FinalLine, int Count);
}
