using System.Text;

namespace Gitunia.CustomRenderTui;

public enum KeyKind
{
    Text,
    Enter,
    Escape,
    Backspace,
    Tab,
    Up,
    Down,
    Left,
    Right,
    PageUp,
    PageDown,
    Home,
    End,
    WheelUp,
    WheelDown,
}

public readonly record struct KeyEvent(KeyKind Kind, char Ch = '\0', bool Ctrl = false);

/// <summary>Incremental VT input parser: UTF-8 text, Ctrl combos, CSI/SS3 keys, SGR mouse wheel.</summary>
public sealed class InputParser
{
    private const int MaxPendingBytes = 32;

    private readonly List<byte> _pending = [];

    /// <summary>
    /// Feed the bytes of one read(). A sequence split across two reads is kept and completed by the next call.
    /// A lone trailing ESC is reported as Escape immediately: there is no timeout yet, so ESC followed by a
    /// separate read of '[' is seen as Escape then '['.
    /// </summary>
    public void Feed(ReadOnlySpan<byte> data, List<KeyEvent> output)
    {
        foreach (var b in data)
        {
            _pending.Add(b);
        }

        var i = 0;
        while (i < _pending.Count)
        {
            var n = TryParse(i, output);
            if (n == 0)
            {
                break;
            }

            i += n;
        }

        if (i < _pending.Count && _pending[i] == 0x1b && _pending.Count - i == 1)
        {
            output.Add(new KeyEvent(KeyKind.Escape));
            i++;
        }
        else if (_pending.Count - i > MaxPendingBytes)
        {
            i = _pending.Count; // not a sequence we understand: drop it rather than grow forever
        }

        _pending.RemoveRange(0, i);
    }

    private static void Csi(string args, char fin, List<KeyEvent> o)
    {
        if (args.StartsWith('<'))
        {
            var parts = args[1..].Split(';');
            if (fin == 'M' && parts.Length == 3 && int.TryParse(parts[0], out var btn))
            {
                if (btn == 64)
                {
                    o.Add(new KeyEvent(KeyKind.WheelUp));
                }
                else if (btn == 65)
                {
                    o.Add(new KeyEvent(KeyKind.WheelDown));
                }
            }

            return;
        }

        switch (fin)
        {
            case 'A': o.Add(new KeyEvent(KeyKind.Up)); break;
            case 'B': o.Add(new KeyEvent(KeyKind.Down)); break;
            case 'C': o.Add(new KeyEvent(KeyKind.Right)); break;
            case 'D': o.Add(new KeyEvent(KeyKind.Left)); break;
            case 'H': o.Add(new KeyEvent(KeyKind.Home)); break;
            case 'F': o.Add(new KeyEvent(KeyKind.End)); break;
            case '~':
                switch (args)
                {
                    case "5": o.Add(new KeyEvent(KeyKind.PageUp)); break;
                    case "6": o.Add(new KeyEvent(KeyKind.PageDown)); break;
                    case "1" or "7": o.Add(new KeyEvent(KeyKind.Home)); break;
                    case "4" or "8": o.Add(new KeyEvent(KeyKind.End)); break;
                }

                break;
        }
    }

    // Returns consumed bytes, 0 when the sequence is incomplete.
    private int TryParse(int p, List<KeyEvent> o)
    {
        var b = _pending[p];
        var left = _pending.Count - p;
        if (b == 0x1b)
        {
            if (left < 2)
            {
                return 0;
            }

            var c = _pending[p + 1];
            if (c is (byte)'[' or (byte)'O')
            {
                var j = p + 2;
                while (j < _pending.Count && _pending[j] is >= 0x20 and <= 0x3f)
                {
                    j++;
                }

                if (j >= _pending.Count)
                {
                    return 0;
                }

                var args = Encoding.ASCII.GetString(_pending.GetRange(p + 2, j - p - 2).ToArray());
                Csi(args, (char)_pending[j], o);
                return j - p + 1;
            }

            o.Add(new KeyEvent(KeyKind.Escape));
            return 1;
        }

        if (b is 0x0d or 0x0a)
        {
            o.Add(new KeyEvent(KeyKind.Enter));
            return 1;
        }

        if (b is 0x7f or 0x08)
        {
            o.Add(new KeyEvent(KeyKind.Backspace));
            return 1;
        }

        if (b == 0x09)
        {
            o.Add(new KeyEvent(KeyKind.Tab));
            return 1;
        }

        if (b < 0x20)
        {
            o.Add(new KeyEvent(KeyKind.Text, (char)(b + 0x60), true));
            return 1;
        }

        if (b < 0x80)
        {
            o.Add(new KeyEvent(KeyKind.Text, (char)b));
            return 1;
        }

        var len = b >= 0xf0 ? 4 : b >= 0xe0 ? 3 : 2;
        if (left < len)
        {
            return 0;
        }

        var s = Encoding.UTF8.GetString(_pending.GetRange(p, len).ToArray());
        if (s.Length > 0)
        {
            o.Add(new KeyEvent(KeyKind.Text, s[0]));
        }

        return len;
    }
}
