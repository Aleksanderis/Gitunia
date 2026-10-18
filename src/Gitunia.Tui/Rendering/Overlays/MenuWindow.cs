namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>The visible slice of an actions menu.</summary>
/// <param name="RowCount">Rows shown.</param>
/// <param name="Selected">Index of the highlighted entry.</param>
/// <param name="Top">Index of the first shown entry.</param>
public readonly record struct MenuWindow(int RowCount, int Selected, int Top);
