namespace Gitunia.Tui.Rendering;

/// <summary>One tab as laid out in the tab row: where it starts and what it says.</summary>
/// <param name="Number">One-based tab position.</param>
/// <param name="Start">The first column of the cell.</param>
/// <param name="Text">The text of the cell, not yet cut to the row width.</param>
/// <param name="IsActive">Whether the tab is the active one.</param>
public sealed record TabCell(int Number, int Start, string Text, bool IsActive)
{
    public int Width => TextWidth.Of(Text);
}
