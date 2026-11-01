using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core;

// The pane focus and sidebar keys (#996). The filter being typed is the keyboard's (Insert mode) while it lasts.
public static partial class AppReducer
{
    // A command for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceSidebarAction(AppState state, SidebarActionCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var before = state.Shell.Sidebar;
        var update = SidebarActionReducer.Reduce(before, command, tab);
        var shell = state.Shell with { Sidebar = update.Session };
        if ((before.FilterTab is null) != (update.Session.FilterTab is null))
        {
            shell = shell.WithMode(update.Session.FilterTab is null ? KeyMode.Normal : KeyMode.Insert);
        }

        return ApplyTabCommands(state with { Shell = shell }, tab.Id, update.TabCommands, update.Effects);
    }

    // The filter belongs to the tab it was opened in: it ends when another tab becomes the active one or its tab is
    // closed.
    private static ShellState EndFilterOutsideTab(ShellState shell, Workspace workspace)
    {
        if (shell.Sidebar.FilterTab is not { } owner || workspace.ActiveTabId == owner)
        {
            return shell;
        }

        return (shell with { Sidebar = SidebarSession.Empty }).WithMode(KeyMode.Normal);
    }
}
