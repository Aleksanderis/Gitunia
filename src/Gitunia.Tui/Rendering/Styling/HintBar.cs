using Gitunia.Core.Keymap;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>One entry of a hint row: the key text (bold) and what it does. An empty key makes it a plain note.</summary>
/// <param name="Keys">The key text, such as <c>Enter</c> or <c>j/k</c>; empty for a plain note.</param>
/// <param name="Label">What the key does.</param>
public readonly record struct HintItem(string Keys, string Label)
{
    /// <summary>The text as drawn: the key, a space, the label.</summary>
    public string Text => Keys.Length == 0 ? Label : $"{Keys} {Label}";
}

/// <summary>
/// The one builder of key-plus-label rows (<c>Enter choose   Esc cancel</c>): the status bar, the palette and popup
/// footers, the operation banner and the editors' key lines all draw their hints through it, so a key always looks
/// the same: bold accent key, plain description, in the status bar's colours. Clickable chips are drawn by
/// <see cref="KeyedChip"/>, which gives a button the same look.
/// </summary>
public static class HintBar
{
    private const string Margin = " ";
    private const int MinimumGapColumns = 2;
    private const char Space = ' ';

    private static readonly HashSet<string> NamedKeys = new(StringComparer.Ordinal)
    {
        "Enter", "Esc", "Tab", "Space", "Backspace", "Delete", "Up", "Down", "Left", "Right", "Home", "End",
        "PageUp", "PageDown",
    };

    private static readonly string[] KeyPrefixes = ["Ctrl-", "Alt-", "Shift-", "<"];

    /// <summary>Appends <paramref name="items"/> separated by <paramref name="gap"/>, starting with a one-column
    /// margin.</summary>
    /// <param name="builder">The line being built.</param>
    /// <param name="items">The hints, left to right.</param>
    /// <param name="gap">The text between two hints.</param>
    public static void Append(LineBuilder builder, IEnumerable<HintItem> items, string gap)
    {
        var first = true;
        foreach (var item in items)
        {
            builder.Append(first ? Margin : gap, SegmentRole.Status);
            AppendOne(builder, item);
            first = false;
        }
    }

    /// <summary>Appends one hint without margin or gap: its key in the key style, then its label.</summary>
    public static void AppendOne(LineBuilder builder, HintItem item)
    {
        if (item.Keys.Length == 0)
        {
            builder.Append(item.Label, SegmentRole.Status);
            return;
        }

        builder.Append(KeyFormatter.BreakLigatures(item.Keys), SegmentRole.StatusKey);
        builder.Append($" {item.Label}", SegmentRole.Status);
    }

    /// <summary>
    /// Reads a hint text written as <c>key label</c> entries separated by two or more spaces
    /// (<c>Enter choose   Esc cancel</c>). An entry whose first word is not a key (see <see cref="IsKey"/>) is a
    /// plain note.
    /// </summary>
    public static IReadOnlyList<HintItem> Parse(string text)
    {
        var items = new List<HintItem>();
        var start = 0;
        while (start < text.Length)
        {
            var end = EndOfEntry(text, start);
            var entry = text[start..end].Trim();
            if (entry.Length > 0)
            {
                items.Add(ToItem(entry));
            }

            start = end;
            while (start < text.Length && text[start] == Space)
            {
                start++;
            }
        }

        return items;
    }

    /// <summary>
    /// Whether <paramref name="word"/> reads as a key: a named key (<c>Enter</c>, <c>Up</c>), a chord prefix
    /// (<c>Ctrl-g</c>, <c>&lt;space&gt;o</c>), one character, or anything with a digit or punctuation (<c>j/k</c>,
    /// <c>+/-</c>, <c>1-9</c>). Ordinary words are not.
    /// </summary>
    public static bool IsKey(string word) =>
        word.Length == 1
        || NamedKeys.Contains(word)
        || KeyPrefixes.Any(prefix => word.StartsWith(prefix, StringComparison.Ordinal))
        || word.Any(character => !char.IsLetter(character));

    private static int EndOfEntry(string text, int start)
    {
        var index = start;
        while (index < text.Length)
        {
            var run = 0;
            while (index + run < text.Length && text[index + run] == Space)
            {
                run++;
            }

            if (run >= MinimumGapColumns)
            {
                return index;
            }

            index += Math.Max(1, run);
        }

        return text.Length;
    }

    private static HintItem ToItem(string entry)
    {
        var space = entry.IndexOf(Space, StringComparison.Ordinal);
        var word = space < 0 ? entry : entry[..space];

        return space > 0 && IsKey(word)
            ? new HintItem(word, entry[(space + 1)..].TrimStart())
            : new HintItem(string.Empty, entry);
    }
}
