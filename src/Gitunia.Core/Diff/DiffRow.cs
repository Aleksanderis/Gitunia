namespace Gitunia.Core.Diff;

/// <summary>One row of the diff pane, without any width decisions: the renderer cuts it to the pane.</summary>
/// <param name="Kind">What the row is.</param>
/// <param name="Text">The text without a diff prefix character.</param>
/// <param name="OldLineNumber">Line number on the old side, null when the row has none.</param>
/// <param name="NewLineNumber">Line number on the new side, null when the row has none.</param>
/// <param name="FileIndex">Index of the file the row belongs to, -1 for rows above the first file.</param>
public sealed record DiffRow(DiffRowKind Kind, string Text, int? OldLineNumber, int? NewLineNumber, int FileIndex);
