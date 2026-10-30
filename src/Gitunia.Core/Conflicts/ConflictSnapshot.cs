namespace Gitunia.Core.Conflicts;

/// <summary>What an undo or redo restores: the result buffer and the conflict under the cursor.</summary>
/// <param name="Buffer">The result text, regions and cursor.</param>
/// <param name="SelectedIndex">The conflict under the cursor.</param>
public sealed record ConflictSnapshot(ConflictBufferSnapshot Buffer, int SelectedIndex);
