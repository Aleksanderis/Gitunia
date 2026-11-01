using Gitunia.Core.Worktrees;

namespace Gitunia.Tui.App.Worktrees;

/// <summary>Says what the worktree actions act on right now: the sidebar row under the cursor and the commit selected
/// in the graph.</summary>
public interface IWorktreeSelectionSource
{
    /// <summary>The selection as the user sees it at this moment.</summary>
    WorktreeTarget Current();
}
