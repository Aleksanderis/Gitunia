using System.Text;

namespace Gitunia.CustomRenderTui.Demo;

/// <summary>Compact append-only commit lines ("hash\tsubject" as UTF-8) with an offset index. UI-thread only.</summary>
public sealed class CommitStore
{
    private byte[] _data = new byte[1 << 20];
    private int _len;
    private int[] _starts = new int[1 << 14];
    private readonly List<int> _ends = [];
    private int _scanned;

    public int Count { get; private set; }

    public void Append(ReadOnlySpan<byte> chunk)
    {
        if (_len + chunk.Length > _data.Length)
        {
            Array.Resize(ref _data, Math.Max(_data.Length * 2, _len + chunk.Length));
        }

        chunk.CopyTo(_data.AsSpan(_len));
        _len += chunk.Length;

        // index complete lines only; a partial trailing line waits for the next chunk
        var lineStart = Count == 0 ? 0 : _starts[Count - 1] + LineLength(Count - 1) + 1;
        for (var i = Math.Max(_scanned, lineStart); i < _len; i++)
        {
            if (_data[i] == (byte)'\n')
            {
                if (Count == _starts.Length)
                {
                    Array.Resize(ref _starts, _starts.Length * 2);
                }

                _starts[Count++] = lineStart;
                _ends.Add(i);
                lineStart = i + 1;
            }
        }

        _scanned = _len;
    }

    private int LineLength(int i) => _ends[i] - _starts[i];

    public ReadOnlySpan<byte> Line(int i) => _data.AsSpan(_starts[i], LineLength(i));

    public string Hash(int i) => Encoding.UTF8.GetString(Field(i, 0));

    public string Subject(int i) => Encoding.UTF8.GetString(Field(i, 2));

    /// <summary>Field n of line i: 0 hash, 1 parents, 2 subject ("hash TAB parents TAB subject").</summary>
    public ReadOnlySpan<byte> Field(int i, int n)
    {
        var line = Line(i);
        for (var f = 0; f < n; f++)
        {
            var t = line.IndexOf((byte)'\t');
            if (t < 0)
            {
                return [];
            }

            line = line[(t + 1)..];
        }

        if (n < 2)
        {
            var end = line.IndexOf((byte)'\t');
            return end < 0 ? line : line[..end];
        }

        return line;
    }

    public string Text(int i) => Encoding.UTF8.GetString(Line(i)).Replace('\t', ' ');
}
