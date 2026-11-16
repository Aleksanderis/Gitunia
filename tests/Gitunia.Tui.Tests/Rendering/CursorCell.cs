using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Tests.Rendering;

/// <summary>Reads the cursor cell of a drawn text field.</summary>
internal static class CursorCell
{
    /// <summary>The text of the line's <see cref="SegmentRole.Cursor"/> segments (empty when it has none).</summary>
    public static string Of(StyledLine line) =>
        string.Concat(line.Segments.Where(segment => segment.Role == SegmentRole.Cursor).Select(segment => segment.Text));
}
