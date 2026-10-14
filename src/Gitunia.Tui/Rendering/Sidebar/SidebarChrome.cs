namespace Gitunia.Tui.Rendering.Sidebar;

/// <summary>The sidebar state the views around the list need: the filter line and whether the divider is highlighted.</summary>
/// <param name="FilterText">The name filter, empty when none.</param>
/// <param name="IsEditingFilter">The filter is being typed.</param>
/// <param name="IsFocused">The sidebar has focus.</param>
public sealed record SidebarChrome(string FilterText, bool IsEditingFilter, bool IsFocused)
{
    /// <summary>No filter, not editing, not focused.</summary>
    public static SidebarChrome Idle { get; } = new(string.Empty, false, false);
}
