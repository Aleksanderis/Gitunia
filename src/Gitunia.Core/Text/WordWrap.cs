namespace Gitunia.Core.Text;

/// <summary>Greedy word wrap by display width.</summary>
public static class WordWrap
{
    /// <summary>
    /// Wraps <paramref name="text"/> to <paramref name="columns"/>: whole words per line; a word longer than a line is
    /// cut between runes, keeping combining marks with their base. Always returns at least one line.
    /// </summary>
    public static List<string> Lines(string text, int columns)
    {
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;
            while (DisplayWidth.Of(remaining) > columns)
            {
                if (current.Length > 0)
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                var length = 0;
                var width = 0;
                var counter = new WidthCounter();
                foreach (var rune in remaining.EnumerateRunes())
                {
                    var runeWidth = counter.Next(rune);
                    if (length > 0 && width + runeWidth > columns && runeWidth > 0)
                    {
                        break;
                    }

                    length += rune.Utf16SequenceLength;
                    width += runeWidth;
                }

                lines.Add(remaining[..length]);
                remaining = remaining[length..];
            }

            if (current.Length == 0)
            {
                current = remaining;
            }
            else if (DisplayWidth.Of(current) + 1 + DisplayWidth.Of(remaining) <= columns)
            {
                current = $"{current} {remaining}";
            }
            else
            {
                lines.Add(current);
                current = remaining;
            }
        }

        if (current.Length > 0 || lines.Count == 0)
        {
            lines.Add(current);
        }

        return lines;
    }
}
