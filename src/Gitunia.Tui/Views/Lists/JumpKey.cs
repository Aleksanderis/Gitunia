namespace Gitunia.Tui.Views.Lists;

/// <summary>What the quick-jump labels of a frame were worked out from; the labels are reused while it stays the same.</summary>
public readonly record struct JumpKey(string Text, int FirstRow, int RowCount, int CursorRow, int GraphCount, int FirstRowHash);
