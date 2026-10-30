namespace Gitunia.Tui.Rendering.Styling;

/// <summary>How a segment is selected when that differs from its line (a text frame made of several panes side by side).</summary>
public enum SegmentSelection
{
    /// <summary>As the line: selected (in a focused pane) when <see cref="StyledLine.IsSelected"/> is set.</summary>
    Inherit,

    /// <summary>Not selected, whatever the line says.</summary>
    None,

    /// <summary>Selected in a pane with focus.</summary>
    Focused,

    /// <summary>Selected in a pane without focus.</summary>
    Dim,

    /// <summary>In a Visual range (the stronger range tint), whatever the focus.</summary>
    Range,
}
