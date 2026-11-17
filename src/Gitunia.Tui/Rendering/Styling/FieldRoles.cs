namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// The roles of the parts of a text field. A field never picks its own: typed text is always a typed-text role, so it
/// looks the same in every field; only the surface it sits on (a body row, the status bar or a footer bar, a title
/// bar) changes the background it needs.
/// </summary>
/// <param name="TypedText">Role of the typed text.</param>
/// <param name="Prefix">Role of the fixed text around the field (the prefix and the suffix).</param>
/// <param name="Placeholder">Role of the hint shown while the field is empty.</param>
/// <param name="Caret">Role of the caret cell.</param>
public sealed record FieldRoles(SegmentRole TypedText, SegmentRole Prefix, SegmentRole Placeholder, SegmentRole Caret)
{
    /// <summary>A field in a body row of a pane or an overlay.</summary>
    public static FieldRoles Body { get; } =
        new(SegmentRole.TypedText, SegmentRole.Plain, SegmentRole.Faint, SegmentRole.Cursor);

    /// <summary>A field on the status bar or a footer bar (the find bars, the picker and reflog footers).</summary>
    public static FieldRoles Bar { get; } =
        new(SegmentRole.BarTypedText, SegmentRole.Status, SegmentRole.Status, SegmentRole.Cursor);

    /// <summary>A field in a title bar (the help filter, the palette): the chip's colours, the caret swapped.</summary>
    public static FieldRoles Title { get; } =
        new(SegmentRole.OverlayTitle, SegmentRole.OverlayTitle, SegmentRole.OverlayTitle, SegmentRole.TitleCursor);
}
