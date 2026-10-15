using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>Where the panes of the working-tree view sit (content rectangles; a pane that is not shown is empty).</summary>
/// <param name="Unstaged">The Unstaged file list.</param>
/// <param name="Staged">The Staged file list.</param>
/// <param name="Diff">The diff of the selected file (a placeholder until the diff pane arrives).</param>
/// <param name="Message">The commit message box (a placeholder until the editor arrives).</param>
public sealed record WorkingTreeRects(PaneRect Unstaged, PaneRect Staged, PaneRect Diff, PaneRect Message);
