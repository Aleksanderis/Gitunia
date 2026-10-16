using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Tabs;

/// <summary>One tab as laid out in the tab row: where it starts and what it says.</summary>
/// <param name="Number">One-based tab position.</param>
/// <param name="Start">The first column of the cell.</param>
/// <param name="Text">The text of the cell, not yet cut to the row width.</param>
/// <param name="IsActive">Whether the tab is the active one.</param>
/// <param name="DirtyOffset">The column of the dirty marker relative to <paramref name="Start"/>, or null when the tab shows none.</param>
public sealed record TabCell(int Number, int Start, string Text, bool IsActive, int? DirtyOffset = null)
{
    public int Width => TextWidth.Of(Text);
}
