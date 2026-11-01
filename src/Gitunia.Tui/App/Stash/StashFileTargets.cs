using Gitunia.Core.Stash;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Tui.Rendering.Details;

namespace Gitunia.Tui.App.Stash;

/// <summary>Finds the stash file that the cursor of the detail pane's Changes list is on.</summary>
public static class StashFileTargets
{
    /// <summary>The file under the cursor of the shown stash.</summary>
    /// <param name="shown">The stash the pane shows, null for a commit.</param>
    /// <param name="content">What the pane lists.</param>
    /// <param name="cursorRow">The selected row of the detail list.</param>
    /// <param name="untrackedCount">How many of the last files were saved untracked files.</param>
    /// <returns>Null when no stash is shown or the cursor is not on a file row.</returns>
    public static StashFileTarget? Resolve(
        StashEntry? shown,
        DetailContent content,
        int cursorRow,
        int untrackedCount)
    {
        if (shown is null || content.Tab != BottomTab.Changes)
        {
            return null;
        }

        var index = content.FileIndexAt(cursorRow);
        if (index < 0 || index >= content.Files.Count)
        {
            return null;
        }

        var untrackedFrom = content.Files.Count - untrackedCount;
        var origin = index >= untrackedFrom ? StashFileOrigin.Untracked : StashFileOrigin.Tracked;
        var change = content.Files[index];

        return new StashFileTarget(shown, new StashFileChange(change, origin));
    }
}
