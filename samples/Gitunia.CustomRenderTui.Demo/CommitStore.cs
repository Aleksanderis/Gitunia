using System.Text;

namespace Gitunia.CustomRenderTui.Demo;

/// <summary>Compact append-only commit lines ("hash\tsubject" as UTF-8) with an offset index. UI-thread only.</summary>
public sealed class CommitStore
{
    private byte[] _data = new byte[1 << 20];
    private int _length;
    private int[] _starts = new int[1 << 14];
    private readonly List<int> _ends = [];
    private int _scanned;

    public int Count { get; private set; }

    public void Append(ReadOnlySpan<byte> chunk)
    {
        if (_length + chunk.Length > _data.Length)
        {
            Array.Resize(ref _data, Math.Max(_data.Length * 2, _length + chunk.Length));
        }

        chunk.CopyTo(_data.AsSpan(_length));
        _length += chunk.Length;

        // index complete lines only; a partial trailing line waits for the next chunk
        var lineStart = Count == 0 ? 0 : _starts[Count - 1] + LineLength(Count - 1) + 1;
        for (var i = Math.Max(_scanned, lineStart); i < _length; i++)
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

        _scanned = _length;
    }

    private int LineLength(int index) => _ends[index] - _starts[index];

    public ReadOnlySpan<byte> Line(int index) => _data.AsSpan(_starts[index], LineLength(index));

    public string Hash(int index) => Encoding.UTF8.GetString(Field(index, 0));

    public string Subject(int index) => Encoding.UTF8.GetString(Field(index, 2));

    /// <summary>Field fieldIndex of line index: 0 hash, 1 parents, 2 subject ("hash TAB parents TAB subject").</summary>
    public ReadOnlySpan<byte> Field(int index, int fieldIndex)
    {
        var line = Line(index);
        for (var skipped = 0; skipped < fieldIndex; skipped++)
        {
            var tabIndex = line.IndexOf((byte)'	');
            if (tabIndex < 0)
            {
                return [];
            }

            line = line[(tabIndex + 1)..];
        }

        if (fieldIndex < 2)
        {
            var end = line.IndexOf((byte)'\t');
            return end < 0 ? line : line[..end];
        }

        return line;
    }

    public string Text(int index) => Encoding.UTF8.GetString(Line(index)).Replace('\t', ' ');
}
