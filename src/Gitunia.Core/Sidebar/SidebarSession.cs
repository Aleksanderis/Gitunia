using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Sidebar;

/// <summary>What the sidebar keys keep between presses: the tab whose name filter is being typed.</summary>
/// <param name="FilterTab">The tab of the filter being typed (Insert mode is on), null when none is.</param>
public sealed record SidebarSession(TabId? FilterTab)
{
    /// <summary>No filter is being typed.</summary>
    public static SidebarSession Empty { get; } = new((TabId?)null);

    /// <summary>The filter of this tab is being typed.</summary>
    public bool IsEditing(TabId tab) => FilterTab == tab;
}
