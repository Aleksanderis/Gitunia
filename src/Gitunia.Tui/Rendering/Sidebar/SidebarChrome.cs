using Gitunia.Core;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Rendering.Sidebar;

/// <summary>The sidebar state the views around the list need: the filter line and whether the divider is highlighted.</summary>
/// <param name="FilterText">The name filter, empty when none.</param>
/// <param name="IsEditingFilter">The filter is being typed.</param>
/// <param name="IsFocused">The sidebar has focus.</param>
// ReSharper disable once NotAccessedPositionalProperty.Global : Part of the full value, not read yet
public sealed record SidebarChrome(string FilterText, bool IsEditingFilter, bool IsFocused)
{
    /// <summary>No filter, not editing, not focused.</summary>
    public static SidebarChrome Idle { get; } = new(string.Empty, false, false);

    /// <summary>The chrome of the active tab in a state.</summary>
    public static SidebarChrome Of(AppState state)
    {
        if (state.Workspace.ActiveTab is not { } tab)
        {
            return Idle;
        }

        var isEditing = state.Shell.Sidebar.IsEditing(tab.Id);

        return new SidebarChrome(tab.Sidebar.FilterText, isEditing, tab.View.FocusedPane == PaneKind.Sidebar);
    }
}
