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

public readonly record struct KeyEvent(KeyKind Kind, char Character = '\0', bool Ctrl = false);

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
        foreach (var value in data)
        {
            _pending.Add(value);
        }

        var i = 0;
        while (i < _pending.Count)
        {
            var consumed = TryParse(i, output);
            if (consumed == 0)
            {
                break;
            }

            i += consumed;
        }

        if (i < _pending.Count && _pending[i] == 0x1b && _pending.Count - i == 1)
        {
            output.Add(new KeyEvent(KeyKind.Escape));
            i++;
        }
        else if (_pending.Count - i > MaxPendingBytes)
        {
            i = _pending.Count; // Not a sequence we understand: drop it rather than grow forever
        }

        _pending.RemoveRange(0, i);
    }

    private static void Csi(string args, char finalChar, List<KeyEvent> output)
    {
        if (args.StartsWith('<'))
        {
            var parts = args[1..].Split(';');
            if (finalChar == 'M' && parts.Length == 3 && int.TryParse(parts[0], out var button))
            {
                if (button == 64)
                {
                    output.Add(new KeyEvent(KeyKind.WheelUp));
                }
                else if (button == 65)
                {
                    output.Add(new KeyEvent(KeyKind.WheelDown));
                }
            }

            return;
        }

        switch (finalChar)
        {
            case 'A': output.Add(new KeyEvent(KeyKind.Up)); break;
            case 'B': output.Add(new KeyEvent(KeyKind.Down)); break;
            case 'C': output.Add(new KeyEvent(KeyKind.Right)); break;
            case 'D': output.Add(new KeyEvent(KeyKind.Left)); break;
            case 'H': output.Add(new KeyEvent(KeyKind.Home)); break;
            case 'F': output.Add(new KeyEvent(KeyKind.End)); break;
            case '~':
                switch (args)
                {
                    case "5": output.Add(new KeyEvent(KeyKind.PageUp)); break;
                    case "6": output.Add(new KeyEvent(KeyKind.PageDown)); break;
                    case "1" or "7": output.Add(new KeyEvent(KeyKind.Home)); break;
                    case "4" or "8": output.Add(new KeyEvent(KeyKind.End)); break;
                }

                break;
        }
    }

    // Returns consumed bytes, 0 when the sequence is incomplete.
    private int TryParse(int start, List<KeyEvent> output)
    {
        var first = _pending[start];
        var left = _pending.Count - start;
        if (first == 0x1b)
        {
            if (left < 2)
            {
                return 0;
            }

            var second = _pending[start + 1];
            if (second is (byte)'[' or (byte)'O')
            {
                var end = start + 2;
                while (end < _pending.Count && _pending[end] is >= 0x20 and <= 0x3f)
                {
                    end++;
                }

                if (end >= _pending.Count)
                {
                    return 0;
                }

                var args = Encoding.ASCII.GetString([.. _pending.GetRange(start + 2, end - start - 2)]);
                Csi(args, (char)_pending[end], output);
                return end - start + 1;
            }

            output.Add(new KeyEvent(KeyKind.Escape));
            return 1;
        }

        if (first is 0x0d or 0x0a)
        {
            output.Add(new KeyEvent(KeyKind.Enter));
            return 1;
        }

        if (first is 0x7f or 0x08)
        {
            output.Add(new KeyEvent(KeyKind.Backspace));
            return 1;
        }

        if (first == 0x09)
        {
            output.Add(new KeyEvent(KeyKind.Tab));
            return 1;
        }

        if (first < 0x20)
        {
            output.Add(new KeyEvent(KeyKind.Text, (char)(first + 0x60), true));
            return 1;
        }

        if (first < 0x80)
        {
            output.Add(new KeyEvent(KeyKind.Text, (char)first));
            return 1;
        }

        var length = first >= 0xf0 ? 4 : first >= 0xe0 ? 3 : 2;
        if (left < length)
        {
            return 0;
        }

        var text = Encoding.UTF8.GetString([.. _pending.GetRange(start, length)]);
        if (text.Length > 0)
        {
            output.Add(new KeyEvent(KeyKind.Text, text[0]));
        }

        return length;
    }
}
