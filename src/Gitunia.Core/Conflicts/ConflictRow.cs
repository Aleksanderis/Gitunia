namespace Gitunia.Core.Conflicts;

/// <summary>One aligned row of the three version panes: the same row of ours, theirs and the base.</summary>
/// <param name="Part">Index of the file part (common text or conflict) the row belongs to.</param>
/// <param name="Offset">Zero-based line inside the part.</param>
/// <param name="Hunk">Index of the conflict, -1 for common text.</param>
public readonly record struct ConflictRow(int Part, int Offset, int Hunk);
