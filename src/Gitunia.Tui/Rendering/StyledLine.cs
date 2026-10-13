namespace Gitunia.Tui.Rendering;

/// <summary>One line of the screen: styled segments that together fill exactly the line width.</summary>
/// <param name="Segments">The pieces, left to right.</param>
/// <param name="IsSelected">The row is the selection of its list (the view highlights the whole row).</param>
public sealed record StyledLine(IReadOnlyList<StyledSegment> Segments, bool IsSelected = false)
{
    /// <summary>The line as plain text (what a snapshot test compares).</summary>
    public string Text => string.Concat(Segments.Select(segment => segment.Text));
}
