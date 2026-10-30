using Gitunia.Core.Conflicts;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Where the parts of the full-screen conflict editor sit at a terminal size.</summary>
/// <param name="Width">Columns of the frame, the whole terminal width.</param>
/// <param name="Panes">The version panes shown, left to right (fewer than three on a narrow terminal).</param>
/// <param name="VersionRows">Rows of each version pane.</param>
/// <param name="ResultRows">Rows of the result pane.</param>
/// <param name="ChipRows">The clickable key hints under the result, wrapped to the width.</param>
/// <param name="FirstChipLine">Frame line of the first chip row.</param>
public sealed record ConflictEditorLayout(
    int Width,
    IReadOnlyList<ConflictPane> Panes,
    int VersionRows,
    int ResultRows,
    IReadOnlyList<List<HintChip>> ChipRows,
    int FirstChipLine)
{
    /// <summary>The frame line of the first version pane row.</summary>
    public const int VersionStartLine = 2;

    /// <summary>The frame line of the first result row.</summary>
    public int ResultStartLine => VersionStartLine + VersionRows + 2;
}
