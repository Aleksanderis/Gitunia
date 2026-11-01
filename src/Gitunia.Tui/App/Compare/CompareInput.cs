using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.Core.Compare;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.App.Compare;

/// <summary>
/// The terminal side of the comparison of two commits (#535, #636): it turns the compare key and the Esc that clears the
/// mark into commands, and reports each change of the details store, with the graph positions the reducer needs. The
/// reducer decides; the details store is driven by effects.
/// </summary>
/// <param name="host">Takes the commands and holds the state.</param>
/// <param name="source">The active session and the graph cursor.</param>
public sealed class CompareInput(Dispatcher host, ICompareSource source)
{
    /// <summary>Handles the compare key and the Esc that clears the mark; false for every other action.</summary>
    public bool TryExecute(ActionId action)
    {
        if (action == ActionIds.GraphCompare)
        {
            if (host.Workspace.ActiveTab is { } tab && source.ActiveSession() is not null)
            {
                host.Dispatch(new CompareMarkPressed(tab.Id, source.SelectedCommitId()));
            }

            return true;
        }

        if (action == ActionIds.Cancel
            && host.Workspace.ActiveTab is { } marked
            && CompareReducer.ClaimsEscape(marked.View.CompareMark, marked.View.FocusedPane))
        {
            host.Dispatch(new CompareEscapePressed(marked.Id));

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reports what the details store of the active tab shows, so the commit under the cursor is compared with the
    /// mark once its own details have loaded (#636). Called when the store changes; does nothing without a mark. The
    /// own details load goes through the store's debounce, so holding a movement key starts no process per row.
    /// </summary>
    public void Follow()
    {
        if (host.Workspace.ActiveTab is not { View.CompareMark: { } mark } tab
            || source.ActiveSession() is not { } session)
        {
            return;
        }

        var current = session.Details.Current;
        var commitIndex = current.Commit is { } commit ? session.Graph.IndexOf(commit) : -1;
        var facts = new CompareFacts(
            current.Status == CommitDetailsStatus.Loaded,
            current.Commit,
            current.Details?.IsComparison == true,
            session.Graph.IndexOf(mark),
            commitIndex);
        host.Dispatch(new CompareDetailsChanged(tab.Id, facts));
    }
}
