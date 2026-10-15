using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Diff;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>What the working-tree view shows. Plain data, so a frame renders without a terminal.</summary>
/// <param name="Tree">The status and the selection, null while the tab has not read its status.</param>
/// <param name="Focused">The focused pane: one of the two lists or one of the two diffs.</param>
/// <param name="UnstagedTop">Index of the first visible Unstaged row.</param>
/// <param name="StagedTop">Index of the first visible Staged row.</param>
/// <param name="Diff">The diff of the selected file, null when there is none (blank diff box).</param>
public sealed record WorkingTreeModel(
    WorkingTreeState? Tree,
    PaneKind Focused,
    int UnstagedTop = 0,
    int StagedTop = 0,
    DiffPaneModel? Diff = null);
