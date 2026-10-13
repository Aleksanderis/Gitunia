using System.Text;

namespace Gitunia.Git;

/// <summary>Reads the path syntax of diff headers: C-style quoting and the ambiguous <c>diff --git a/x b/y</c> line.</summary>
public static class DiffPathText
{
    private const string OldPrefix = "a/";
    private const string NewPrefix = "b/";

    /// <summary>Undoes git's quoting (<c>"a\tb\303\274"</c>); text that is not quoted is returned unchanged.</summary>
    public static string Unquote(string text)
    {
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            return text;
        }

        var bytes = new List<byte>(text.Length);
        var index = 1;
        while (index < text.Length - 1)
        {
            var character = text[index];
            if (character != '\\' || index + 1 >= text.Length - 1)
            {
                AppendCharacter(bytes, text, ref index);
                continue;
            }

            var escaped = text[index + 1];
            if (escaped is >= '0' and <= '7')
            {
                var value = 0;
                var digits = 0;
                index++;
                while (digits < 3 && index < text.Length - 1 && text[index] is >= '0' and <= '7')
                {
                    value = (value * 8) + (text[index] - '0');
                    index++;
                    digits++;
                }

                bytes.Add((byte)value);
                continue;
            }

            index += 2;
            bytes.Add(escaped switch
            {
                'a' => 7,
                'b' => 8,
                'f' => 12,
                'n' => 10,
                'r' => 13,
                't' => 9,
                'v' => 11,
                _ => (byte)escaped,
            });
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    /// <summary>Removes the <c>a/</c> or <c>b/</c> prefix; <c>/dev/null</c> becomes null.</summary>
    public static string? StripPrefix(string path, bool oldSide)
    {
        if (path == "/dev/null")
        {
            return null;
        }

        var prefix = oldSide ? OldPrefix : NewPrefix;
        return path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..] : path;
    }

    /// <summary>
    /// Splits the text after <c>diff --git </c>. Exact when either path is quoted or both are equal (anything but a
    /// rename or copy); for a rename or copy the result is a guess that the <c>rename from/to</c> lines replace.
    /// </summary>
    public static (string OldPath, string NewPath)? SplitHeader(string text)
    {
        if (text.StartsWith('"'))
        {
            var close = ClosingQuote(text);
            if (close < 0 || close + 2 > text.Length || text[close + 1] != ' ')
            {
                return null;
            }

            return (StripPrefix(Unquote(text[..(close + 1)]), true) ?? string.Empty, StripPrefix(Unquote(text[(close + 2)..]), false) ?? string.Empty);
        }

        if (text.EndsWith('"'))
        {
            var open = text.LastIndexOf(" \"", StringComparison.Ordinal);
            if (open < 0)
            {
                return null;
            }

            return (StripPrefix(text[..open], true) ?? string.Empty, StripPrefix(Unquote(text[(open + 1)..]), false) ?? string.Empty);
        }

        if (text.Length >= OldPrefix.Length + 1 + NewPrefix.Length && text.Length % 2 == 1)
        {
            var middle = text.Length / 2;
            if (text[middle] == ' ' && text.AsSpan(OldPrefix.Length, middle - OldPrefix.Length).SequenceEqual(text.AsSpan(middle + 1 + NewPrefix.Length)))
            {
                return (StripPrefix(text[..middle], true) ?? string.Empty, StripPrefix(text[(middle + 1)..], false) ?? string.Empty);
            }
        }

        var separator = text.IndexOf(" b/", StringComparison.Ordinal);
        return separator < 0 ? null : (StripPrefix(text[..separator], true) ?? string.Empty, StripPrefix(text[(separator + 1)..], false) ?? string.Empty);
    }

    private static int ClosingQuote(string text)
    {
        for (var index = 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
            }
            else if (text[index] == '"')
            {
                return index;
            }
        }

        return -1;
    }

    private static void AppendCharacter(List<byte> bytes, string text, ref int index)
    {
        var length = char.IsHighSurrogate(text[index]) && index + 1 < text.Length ? 2 : 1;
        bytes.AddRange(Encoding.UTF8.GetBytes(text.Substring(index, length)));
        index += length;
    }
}
