namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// Cuts a drawn chip label into its padding or bracket columns, its key text and its description, so the key can be
/// styled like a status bar hint.
/// </summary>
public static class ChipSegments
{
    /// <summary>
    /// The pieces of <paramref name="label"/> (already in the glyph tier's form, see <see cref="ButtonLabel"/>) whose
    /// key text starts one column in and is <paramref name="keyLength"/> columns long. Without a key the label is one
    /// piece in <paramref name="plain"/>.
    /// </summary>
    /// <param name="label">The text as drawn: padding or bracket, key, space, description, padding or bracket.</param>
    /// <param name="keyLength">Length of the key text, 0 when the chip shows none.</param>
    /// <param name="plain">Role of a chip without a key.</param>
    /// <param name="edge">Role of the padding or bracket columns of a chip with a key.</param>
    /// <param name="key">Role of the key text.</param>
    /// <param name="description">Role of the description.</param>
    public static IReadOnlyList<StyledSegment> Split(
        string label,
        int keyLength,
        SegmentRole plain,
        SegmentRole edge,
        SegmentRole key,
        SegmentRole description)
    {
        if (keyLength <= 0 || label.Length < keyLength + 3)
        {
            return [new StyledSegment(label, plain)];
        }

        return
        [
            new StyledSegment(label[..1], edge),
            new StyledSegment(label.Substring(1, keyLength), key),
            new StyledSegment(label[(1 + keyLength)..^1], description),
            new StyledSegment(label[^1..], edge),
        ];
    }
}
