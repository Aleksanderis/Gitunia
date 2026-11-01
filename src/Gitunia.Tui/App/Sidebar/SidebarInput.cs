using Gitunia.App;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;

namespace Gitunia.Tui.App.Sidebar;

/// <summary>
/// The terminal side of the pane focus and sidebar keys: it decides whether an action is the sidebar's
/// (<see cref="SidebarActionReducer.Classify"/>) and turns it, with the facts only the layout knows, into a command.
/// The reducer decides what happens; the clipboard, the stash view and the graph cursor are effects.
/// </summary>
/// <param name="host">Takes the commands and holds the state.</param>
/// <param name="layout">The open panes and the height of the sidebar.</param>
public sealed class SidebarInput(Dispatcher host, ISidebarLayout layout)
{
    /// <summary>Returns true when the action was handled here and the graph view must not act on it.</summary>
    public bool TryHandle(ActionId action, int? count)
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var editing = host.State.Shell.Sidebar.IsEditing(tab.Id);
        if (SidebarActionReducer.Classify(action, count, tab.View.FocusedPane, editing) == SidebarActionKind.None)
        {
            return false;
        }

        host.Dispatch(new SidebarActionPressed(tab.Id, action, count, layout.OpenPanes(), layout.SidebarPageHeight));

        return true;
    }

    /// <summary>Edits the filter with a key typed in Insert mode: printable text is appended, Backspace removes the last character.</summary>
    public void OnKey(KeyEvent key)
    {
        if (host.Workspace.ActiveTab is { } tab && host.State.Shell.Sidebar.IsEditing(tab.Id))
        {
            host.Dispatch(new SidebarFilterKeyTyped(tab.Id, key));
        }
    }
}
