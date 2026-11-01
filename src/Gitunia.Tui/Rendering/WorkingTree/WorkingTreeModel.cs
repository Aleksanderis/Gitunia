using Gitunia.Core.Editing;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Diff;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>What the Changes view shows. Plain data, so a frame renders without a terminal.</summary>
/// <param name="Tree">The status and the selection, null while the tab has not read its status.</param>
/// <param name="Focused">The focused pane: one of the two lists, one of the two diffs or the message box.</param>
/// <param name="UnstagedTop">Index of the first visible Unstaged row.</param>
/// <param name="StagedTop">Index of the first visible Staged row.</param>
/// <param name="Diff">The diff of the selected file, null when there is none (blank diff box).</param>
/// <param name="Draft">The tab's commit draft, null when it has none (nothing typed, no option set).</param>
/// <param name="IsEditing">The message box is in Insert mode, so it shows the cursor.</param>
/// <param name="IsCommitting">A commit runs, so the box is read-only.</param>
/// <param name="IsDiffZoomed">The diff has focus and fills the whole view.</param>
public sealed record WorkingTreeModel(
    WorkingTreeState? Tree,
    PaneKind Focused,
    int UnstagedTop = 0,
    int StagedTop = 0,
    DiffPaneModel? Diff = null,
    CommitDraft? Draft = null,
    bool IsEditing = false,
    bool IsDiffZoomed = false,
    bool IsCommitting = false);
