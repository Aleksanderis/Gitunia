namespace Gitunia.Tui.Rendering.Diff;

/// <summary>A stretch of columns of a drawn line.</summary>
/// <param name="From">The first column.</param>
/// <param name="To">The column after the last one.</param>
public readonly record struct DiffTextColumns(int From, int To);
