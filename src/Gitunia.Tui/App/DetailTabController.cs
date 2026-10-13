using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.App;

/// <summary>Switches the tab of the bottom detail pane; the choice is part of the repo tab's view state, so it is saved with the session.</summary>
/// <param name="host">The workspace the tab state lives in.</param>
public sealed class DetailTabController(WorkspaceHost host)
{
    private static readonly int TabCount = Enum.GetValues<BottomTab>().Length;

    /// <summary>Runs the action when it is one of the tab actions.</summary>
    /// <returns>True when the action was a tab action.</returns>
    public bool TryExecute(ActionId action)
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return IsTabAction(action);
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
