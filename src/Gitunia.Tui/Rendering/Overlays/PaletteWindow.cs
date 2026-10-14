namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>The visible slice of the palette list.</summary>
/// <param name="RowCount">Rows shown.</param>
/// <param name="Selected">Index of the selected entry among the visible ones.</param>
/// <param name="Top">Index of the first shown entry.</param>
public readonly record struct PaletteWindow(int RowCount, int Selected, int Top);
