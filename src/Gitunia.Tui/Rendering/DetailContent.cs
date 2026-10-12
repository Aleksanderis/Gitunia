using Gitunia.Git;

namespace Gitunia.Tui.Rendering;

/// <summary>What the detail pane lists for the selected commit: info rows first, then one row per changed file.</summary>
/// <param name="ShortHash">The short hash of the commit, empty when no commit is selected.</param>
/// <param name="Rows">All rows, top to bottom.</param>
/// <param name="FirstFileRow">Index of the first file row; equals the row count when there are no files.</param>
/// <param name="Files">The changed files; file row <c>FirstFileRow + n</c> shows <c>Files[n]</c>.</param>
public sealed record DetailContent(string ShortHash, IReadOnlyList<DetailRow> Rows, int FirstFileRow, IReadOnlyList<CommitFileChange> Files)
{
    public static DetailContent Empty { get; } = new(string.Empty, [], 0, []);
}
