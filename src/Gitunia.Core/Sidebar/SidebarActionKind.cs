namespace Gitunia.Core.Sidebar;

/// <summary>Whose an action is among the pane and sidebar keys.</summary>
public enum SidebarActionKind
{
    /// <summary>Not the sidebar handler's.</summary>
    None,

    /// <summary>Moving focus between panes.</summary>
    Pane,

    /// <summary>Accepting or cancelling the filter being typed.</summary>
    Filter,

    /// <summary>A key of the focused sidebar list.</summary>
    Sidebar,
}
