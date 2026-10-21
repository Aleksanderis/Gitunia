using Gitunia.Tui.Rendering.Graph;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// The one builder of key-plus-label chips for title rows and change groups: the label with its key text one column
/// in (<c>" c ‹ hunk "</c>) and the pieces it is drawn in. Each place decides, by the room it has, whether a chip
/// shows its key (<see cref="Keyed"/>) or not (<see cref="Plain"/>).
/// </summary>
public static class KeyedChip
{
    /// <summary>The chip with <paramref name="key"/> before its label, or without when the key is null.</summary>
    /// <param name="view">The mouse view of the chip.</param>
    /// <param name="label">The label, padded or not.</param>
    /// <param name="key">The key text, or null for none.</param>
    public static TitleButton Keyed(string view, string label, string? key) => key is null
        ? Plain(view, label)
        : new TitleButton(view, $" {key} {label.Trim()} ", 0, key.Length);

    /// <summary>The chip with its label only, padded.</summary>
    public static TitleButton Plain(string view, string label) => new(view, $" {label.Trim()} ", 0);

    /// <summary>The pieces a chip is drawn in: key in the key style, the rest like the tabs; in the ASCII tier with
    /// brackets.</summary>
    public static IReadOnlyList<StyledSegment> Segments(TitleButton button, GraphGlyphSet glyphs) => ChipSegments.Split(
        ButtonLabel.For(button.Label, glyphs.IsAscii),
        button.KeyLength,
        SegmentRole.RepositoryTab,
        SegmentRole.RepositoryTab,
        SegmentRole.RepositoryTabKey,
        SegmentRole.RepositoryTab);
}
