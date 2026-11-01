namespace Gitunia.Core.Conflicts;

/// <summary>What a click on the conflict editor hit (found by the frame builder of the terminal layer).</summary>
/// <param name="Kind">The kind of target.</param>
/// <param name="Pane">The pane of a header or a version row.</param>
/// <param name="Index">The aligned row of a version row, the result line of a result line.</param>
/// <param name="View">The mouse view of a chip.</param>
/// <param name="Column">The display column in the text of a result line.</param>
public sealed record ConflictTarget(ConflictTargetKind Kind, ConflictPane Pane, int Index, string? View, int Column = 0)
{
    /// <summary>A hint chip with its mouse view.</summary>
    public static ConflictTarget OfChip(string view) => new(ConflictTargetKind.Chip, ConflictPane.Result, 0, view);

    /// <summary>The header line of a pane.</summary>
    public static ConflictTarget OfHeader(ConflictPane pane) => new(ConflictTargetKind.Header, pane, 0, null);

    /// <summary>A row of a version pane, as an aligned row.</summary>
    public static ConflictTarget OfVersionRow(ConflictPane pane, int row) =>
        new(ConflictTargetKind.VersionRow, pane, row, null);

    /// <summary>A line of the result.</summary>
    public static ConflictTarget OfResultLine(int line, int column = 0) =>
        new(ConflictTargetKind.ResultLine, ConflictPane.Result, line, null, column);
}
