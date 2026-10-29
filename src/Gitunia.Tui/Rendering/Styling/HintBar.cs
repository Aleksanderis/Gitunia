using Gitunia.Core.Keymap;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>One entry of a hint row: the key text (bold) and what it does; an empty key is a plain note.</summary>
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
}
