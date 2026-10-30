namespace Gitunia.Core.Conflicts;

/// <summary>The result text, where the conflicts sit in it and the cursor, as an undo or redo restores them.</summary>
/// <param name="Lines">The lines of the result.</param>
/// <param name="Regions">The region of each conflict.</param>
/// <param name="Row">The cursor line.</param>
/// <param name="Column">The cursor column.</param>
public sealed record ConflictBufferSnapshot(
    string[] Lines,
    IReadOnlyList<ConflictRegion> Regions,
    int Row,
    int Column);
