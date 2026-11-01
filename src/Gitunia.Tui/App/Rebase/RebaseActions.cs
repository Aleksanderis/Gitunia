using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Rebase;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;
using Gitunia.Tui.App.Branches;

namespace Gitunia.Tui.App.Rebase;

/// <summary>
/// The terminal side of the rebase actions: it turns <c>RebaseOnto</c> (the selected commit, branch or tag) and the
/// quick fixup, reword and drop (the commit selected in the graph, with the graph focused) into
/// <see cref="RebaseCommand"/>s for the active tab. It decides nothing; the reducer does, and the runner performs the
/// effects. The reword subject is the shared prompt and the questions are the shared confirm dialog.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What a rebase onto a ref acts on.</param>
/// <param name="graph">The commit the quick rebase acts on.</param>
public sealed class RebaseActions(Dispatcher host, IBranchSelectionSource selection, IGraphSelectionSource graph)
    : IActionHandler
{
    /// <summary>Runs the action when it is a rebase action; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var command = Request(tab.Id, action, tab.View.FocusedPane == PaneKind.Graph);
        if (command is null)
        {
            return false;
        }

        host.Dispatch(command);

        return true;
    }

    private RebaseCommand? Request(TabId tabId, ActionId action, bool graphFocused)
    {
        if (action == ActionIds.RebaseOnto)
        {
            var current = selection.Current();

            var chosen = new RebaseSelection(current.RowKind, current.Name, current.CommitHash);

            return new RebaseOntoRequested(tabId, chosen);
        }

        // The quick rebase acts on the graph commit, so it only answers while the graph has focus.
        if (QuickKindOf(action) is { } kind && graphFocused)
        {
            return new QuickRebaseRequested(tabId, kind, graph.SelectedCommit());
        }

        return null;
    }

    private static QuickRebaseKind? QuickKindOf(ActionId action)
    {
        if (action == ActionIds.RebaseFixup)
        {
            return QuickRebaseKind.Fixup;
        }

        if (action == ActionIds.RebaseReword)
        {
            return QuickRebaseKind.Reword;
        }

        return action == ActionIds.RebaseDrop ? QuickRebaseKind.Drop : null;
    }
}
