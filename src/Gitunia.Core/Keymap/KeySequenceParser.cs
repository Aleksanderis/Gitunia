namespace Gitunia.Core.Keymap;

/// <summary>
/// Reads key sequences written the way <c>docs/ux.md</c> writes them. Whitespace separates words. A word is
/// a named key (<c>Enter</c>), a modified key (<c>Ctrl-w</c>, <c>Alt-1</c>, <c>Shift-Tab</c>), or a run of
/// keys where plain characters stand for themselves and <c>&lt;...&gt;</c> holds a special key
/// (<c>&lt;space&gt;o</c>, <c>gg</c>, <c>]c</c>, <c>&lt;lt&gt;</c> for a literal <c>&lt;</c>).
/// </summary>
public static class KeySequenceParser
{
    public static KeySequence Parse(string text)
    {
        return TryParse(text, out var sequence) ? sequence : throw new FormatException($"Invalid key sequence '{text}'.");
    }

    public static bool TryParse(string text, out KeySequence sequence)
    {
        var keys = new List<KeyEvent>();
        sequence = KeySequence.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseWord(word, keys))
            {
                return false;
            }
        }

        if (keys.Count == 0)
        {
            return false;
        }

        sequence = new KeySequence(keys);
        return true;
    }

    private static bool TryParseWord(string word, List<KeyEvent> keys)
    {
        if (!word.Contains('<', StringComparison.Ordinal) && (HasModifierPrefix(word) || KeyNames.IsNamed(word)))
        {
            if (!TryParseKey(word, out var wholeKey))
            {
                return false;
            }

            keys.Add(wholeKey);
            return true;
        }

        var index = 0;
        while (index < word.Length)
        {
            if (word[index] == '<')
            {
                var close = word.IndexOf('>', index + 1);
                if (close < 0 || !TryParseKey(word[(index + 1)..close], out var specialKey))
                {
                    return false;
                }

                keys.Add(specialKey);
                index = close + 1;
                continue;
            }

            var length = char.IsHighSurrogate(word[index]) && index + 1 < word.Length ? 2 : 1;
            keys.Add(new KeyEvent(word.Substring(index, length)));
            index += length;
        }

        return true;
    }

    private static bool HasModifierPrefix(string text)
    {
        return text.StartsWith("ctrl-", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("alt-", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("shift-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseKey(string body, out KeyEvent key)
    {
        key = default;
        var modifiers = KeyModifiers.None;
        var remainder = body;
        while (true)
        {
            if (remainder.StartsWith("ctrl-", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Ctrl;
                remainder = remainder[5..];
            }
            else if (remainder.StartsWith("alt-", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Alt;
                remainder = remainder[4..];
            }
            else if (remainder.StartsWith("shift-", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Shift;
                remainder = remainder[6..];
            }
            else
            {
                break;
            }
        }

        string name;
        if (remainder.Equals("space", StringComparison.OrdinalIgnoreCase))
        {
            name = " ";
        }
        else if (remainder.Equals("lt", StringComparison.OrdinalIgnoreCase))
        {
            name = "<";
        }
        else if (KeyNames.TryGetCanonical(remainder, out var canonical))
        {
            name = canonical;
        }
        else if (remainder.Length == 1 || (remainder.Length == 2 && char.IsSurrogatePair(remainder, 0)))
        {
            name = remainder;
        }
        else
        {
            return false;
        }

        key = new KeyEvent(name, modifiers).Normalize();
        return true;
    }
}
