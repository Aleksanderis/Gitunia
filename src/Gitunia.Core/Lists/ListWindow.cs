namespace Gitunia.Core.Lists;

/// <summary>The visible slice of a list: how many rows show, which item is selected and which item is the first shown.</summary>
/// <param name="RowCount">Rows shown (at least 1).</param>
/// <param name="Selected">Index of the selected item, 0 for an empty list.</param>
/// <param name="Top">Index of the first shown item.</param>
public readonly record struct ListWindow(int RowCount, int Selected, int Top);
