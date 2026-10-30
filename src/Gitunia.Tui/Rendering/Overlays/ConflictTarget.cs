using Gitunia.Core.Conflicts;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>What a click on the conflict editor hit (see <see cref="ConflictEditorFrameBuilder.TargetAt"/>).</summary>
/// <param name="Kind">The kind of target.</param>
/// <param name="Pane">The pane of a header or a version row.</param>
/// <param name="Index">The aligned row of a version row, the result line of a result line.</param>
/// <param name="View">The mouse view of a chip.</param>
public sealed record ConflictTarget(ConflictTargetKind Kind, ConflictPane Pane, int Index, string? View)
{
    /// <summary>A hint chip with its mouse view.</summary>
    public static ConflictTarget OfChip(string view) => new(ConflictTargetKind.Chip, ConflictPane.Result, 0, view);

    /// <summary>The header line of a pane.</summary>
    public static ConflictTarget OfHeader(ConflictPane pane) => new(ConflictTargetKind.Header, pane, 0, null);

    /// <summary>A row of a version pane, as an aligned row.</summary>
    public static ConflictTarget OfVersionRow(ConflictPane pane, int row) =>
        new(ConflictTargetKind.VersionRow, pane, row, null);

    /// <summary>A line of the result.</summary>
    public static ConflictTarget OfResultLine(int line) =>
        new(ConflictTargetKind.ResultLine, ConflictPane.Result, line, null);
}

/// <summary>The kinds of <see cref="ConflictTarget"/>.</summary>
public enum ConflictTargetKind
{
    /// <summary>A hint chip.</summary>
    Chip,

    /// <summary>The header line of a pane.</summary>
    Header,

    /// <summary>A row of a version pane.</summary>
    VersionRow,

    /// <summary>A line of the result.</summary>
    ResultLine,
}
