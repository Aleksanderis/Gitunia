using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// Switches the tab of the bottom detail pane (saved with the session) and the path style of the file lists (per tab,
/// not saved,
/// #540): both are part of the repository tab's view state.
/// </summary>
/// <param name="host">The workspace the tab state lives in.</param>
public sealed class DetailTabController(WorkspaceHost host)
{
    private static readonly int TabCount = Enum.GetValues<BottomTab>().Length;

    /// <summary>The status bar text the last action leaves, empty for none.</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>Runs the action when it is one of the tab actions or the path style toggle.</summary>
    /// <returns>True when the action was one of them.</returns>
    public bool TryExecute(ActionId action)
    {
        Notice = string.Empty;
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return IsTabAction(action) || action == ActionIds.FilesTogglePathStyle;
        }

        if (action == ActionIds.FilesTogglePathStyle)
        {
            var includesTree = tab.View.Mode != TabViewMode.WorkingTree;
            host.Dispatch(new TabCommand(tab.Id, new TogglePathStyleCommand(includesTree)));
            Notice = host.Workspace.ActiveTab?.View.PathStyle switch
            {
                FilePathStyle.FullPath => "File paths: full path",
                FilePathStyle.Tree => "File paths: tree",
                _ => "File paths: name first",
            };

            return true;
        }

        var current = tab.View.BottomTab;
        BottomTab? wanted = null;
        if (action == ActionIds.DetailNextTab)
        {
            wanted = (BottomTab)(((int)current + 1) % TabCount);
        }
        else if (action == ActionIds.DetailPreviousTab)
        {
            wanted = (BottomTab)(((int)current + TabCount - 1) % TabCount);
        }
        else if (action == ActionIds.DetailTabDetails)
        {
            wanted = BottomTab.Details;
        }
        else if (action == ActionIds.DetailTabChanges)
        {
            wanted = BottomTab.Changes;
        }
        else if (action == ActionIds.DetailTabFiles)
        {
            wanted = BottomTab.Files;
        }

        if (wanted is { } next && next != current)
        {
            host.Dispatch(new TabCommand(tab.Id, new SetBottomTabCommand(next)));
        }

        return wanted is not null;
    }

    private static bool IsTabAction(ActionId action) =>
        action == ActionIds.DetailNextTab
        || action == ActionIds.DetailPreviousTab
        || action == ActionIds.DetailTabDetails
        || action == ActionIds.DetailTabChanges
        || action == ActionIds.DetailTabFiles;
}
