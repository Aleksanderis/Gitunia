using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>What the detail pane lists for the selected commit on its selected tab.</summary>
/// <param name="ShortHash">The short hash of the commit, empty when no commit is selected.</param>
/// <param name="Rows">All rows, top to bottom.</param>
/// <param name="FirstFileRow">Index of the first file row; equals the row count when there are no files or the tab lists none.</param>
/// <param name="Files">The changed files (also on tabs that do not list them, for the title); on the Changes tab file row <c>FirstFileRow + n</c> shows <c>Files[n]</c>.</param>
/// <param name="Tab">The tab these rows belong to.</param>
/// <param name="FileCount">The number of changed files, null while the details are not loaded (the title shows <c>.. files</c> then instead of a count that would flicker from 0).</param>
public sealed record DetailContent(string ShortHash, IReadOnlyList<DetailRow> Rows, int FirstFileRow, IReadOnlyList<CommitFileChange> Files, BottomTab Tab, int? FileCount = null)
{
    /// <summary>No commit selected, on the Changes tab.</summary>
    public static DetailContent Empty { get; } = EmptyFor(BottomTab.Changes);

    /// <summary>No commit selected, on <paramref name="tab"/>.</summary>
    public static DetailContent EmptyFor(BottomTab tab) => new(string.Empty, [], 0, [], tab);
}
