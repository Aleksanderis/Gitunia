using Gitunia.Core.Sidebar;

namespace Gitunia.App;

/// <summary>The stash view.</summary>
public interface IStashEffects
{
    /// <summary>Shows the stash of a sidebar row in the detail pane.</summary>
    void ShowStash(SidebarRow row);

    /// <summary>Ends the stash view, if one is shown, and puts the graph selection back in the pane.</summary>
    void LeaveStash();

    /// <summary>Shows the stash under the sidebar cursor when a stash is shown already.</summary>
    void FollowStashCursor();
}
