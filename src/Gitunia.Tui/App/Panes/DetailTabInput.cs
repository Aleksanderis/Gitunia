using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Details;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// The terminal side of the bottom detail pane's tab keys and the path style keys of the file lists: it turns the
/// action into a <see cref="DetailCommand"/> for the active tab. The reducer decides what changes.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
public sealed class DetailTabInput(Dispatcher host) : IActionHandler
{
    /// <summary>Runs the action when it is one of the tab actions or the path style keys.</summary>
    /// <returns>True when the action was one of them.</returns>
    public bool TryExecute(ActionId action)
    {
        var move = MoveOf(action);
        var style = StyleOf(action);
        var toggles = action == ActionIds.FilesTogglePathStyle;
        if (move is null && style is null && !toggles)
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is not { } tabId)
        {
            return true;
        }

        host.Dispatch(move is { } tabMove ? new DetailTabMoved(tabId, tabMove) : new PathStyleChosen(tabId, style));

        return true;
    }

    bool IActionHandler.TryExecute(ActionId action, int? count) => TryExecute(action);

    private static DetailTabMove? MoveOf(ActionId action)
    {
        if (action == ActionIds.DetailNextTab)
        {
            return DetailTabMove.Next;
        }

        if (action == ActionIds.DetailPreviousTab)
        {
            return DetailTabMove.Previous;
        }

        if (action == ActionIds.DetailTabDetails)
        {
            return DetailTabMove.Details;
        }

        if (action == ActionIds.DetailTabChanges)
        {
            return DetailTabMove.Changes;
        }

        return action == ActionIds.DetailTabFiles ? DetailTabMove.Files : null;
    }

    private static FilePathStyle? StyleOf(ActionId action)
    {
        if (action == ActionIds.FilesPathNameFirst)
        {
            return FilePathStyle.NameFirst;
        }

        if (action == ActionIds.FilesPathFullPath)
        {
            return FilePathStyle.FullPath;
        }

        return action == ActionIds.FilesPathTree ? FilePathStyle.Tree : null;
    }
}
