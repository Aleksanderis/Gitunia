using Gitunia.Core.Sidebar;
using Gitunia.Tui.App.Selection;
using Gitunia.Tui.App.Stash;

namespace Gitunia.Tui.App.Shell;

/// <summary>The shell effects of the stash view in the detail pane.</summary>
/// <param name="stashView">The stash view.</param>
/// <param name="rows">The sidebar row under the cursor.</param>
public sealed class StashEffects(StashViewController stashView, RowSelection rows)
{
    /// <summary>Shows the stash of a sidebar row in the detail pane.</summary>
    /// <param name="row">The stash row.</param>
    public void Show(SidebarRow row) => stashView.Show(row);

    /// <summary>Ends the stash view, if one is shown, and puts the graph selection back in the pane.</summary>
    public void Leave() => stashView.Leave();

    /// <summary>Shows the stash under the sidebar cursor when a stash is shown already.</summary>
    public void FollowCursor() => stashView.FollowCursor(rows.CursorRow());
}
