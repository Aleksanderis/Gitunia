using System.Collections.Immutable;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Sidebar;

/// <summary>A pane or sidebar key or the answer of an effect for it, for one tab. Dropped when the tab is gone.</summary>
public abstract record SidebarActionCommand : AppCommand
{
    private protected SidebarActionCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>
/// An action of the pane focus or the sidebar was pressed; <see cref="SidebarActionReducer.Classify"/> said it is the
/// sidebar's. The view says which panes are open and how tall the sidebar is, as it alone knows the layout.
/// </summary>
/// <param name="TabId">The active tab.</param>
/// <param name="Action">The bound action.</param>
/// <param name="Count">The numeric prefix, or null.</param>
/// <param name="OpenPanes">The panes that are shown now.</param>
/// <param name="PageHeight">Visible sidebar rows, for the half-page motions.</param>
public sealed record SidebarActionPressed(
    TabId TabId,
    ActionId Action,
    int? Count,
    ImmutableArray<PaneKind> OpenPanes,
    int PageHeight) : SidebarActionCommand(TabId);

/// <summary>A key was typed while the filter is being edited.</summary>
/// <param name="TabId">The active tab.</param>
/// <param name="Key">The key.</param>
public sealed record SidebarFilterKeyTyped(TabId TabId, KeyEvent Key) : SidebarActionCommand(TabId);

/// <summary>The commit of the activated row is in the graph at an index.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Index">The graph index.</param>
public sealed record SidebarCommitLocated(TabId TabId, int Index) : SidebarActionCommand(TabId);

/// <summary>The commit of the activated row is not in the graph.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Label">The row's name.</param>
/// <param name="IsGraphComplete">The whole graph is loaded, so the commit will not come.</param>
public sealed record SidebarCommitMissing(TabId TabId, string Label, bool IsGraphComplete)
    : SidebarActionCommand(TabId);
