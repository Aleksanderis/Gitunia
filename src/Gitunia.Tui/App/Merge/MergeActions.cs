using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Integration;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Text;
using Gitunia.Git.Merge;
using Gitunia.Git.Refs;
using Gitunia.Tui.App.Branches;

namespace Gitunia.Tui.App.Merge;

/// <summary>
/// The terminal side of the merge keys (<c>gm</c>, <c>gM</c>, <c>gs</c>; ux-workflows.md, workflow 3, "Integrating"):
/// it turns them into a <see cref="MergeRequested"/> for the active tab, carrying what the user sees selected. It
/// decides nothing; the reducer refuses or starts the merge and the runner performs it.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the merge acts on: a sidebar row or the graph commit.</param>
public sealed class MergeActions(Dispatcher host, IBranchSelectionSource selection) : IActionHandler
{
    /// <summary>Runs the action when it is a merge key; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (ModeOf(action) is not { } mode)
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is { } tabId)
        {
            host.Dispatch(new MergeRequested(tabId, mode, TargetOf(selection.Current())));
        }

        return true;
    }

    private static MergeMode? ModeOf(ActionId action)
    {
        if (action == ActionIds.MergeBranch)
        {
            return MergeMode.Default;
        }

        if (action == ActionIds.MergeBranchNoFastForward)
        {
            return MergeMode.NoFastForward;
        }

        return action == ActionIds.MergeBranchSquash ? MergeMode.Squash : null;
    }

    private static MergeTarget? TargetOf(BranchSelection chosen)
    {
        if (chosen.CommitHash is not { } hash)
        {
            return null;
        }

        // A branch is passed by its full ref, so a tag of the same name cannot win; the writer keeps the message
        // "Merge branch 'x'". A commit is passed by hash.
        var shortHash = ShortHash.Of(hash);

        return chosen.RowKind switch
        {
            null => new MergeTarget(hash, shortHash),
            SidebarRowKind.LocalBranch => new MergeTarget($"{GitRefPrefixes.Heads}{chosen.Name}", chosen.Name!),
            SidebarRowKind.RemoteBranch => new MergeTarget($"{GitRefPrefixes.Remotes}{chosen.Name}", chosen.Name!),
            SidebarRowKind.Tag => new MergeTarget(hash, chosen.Name ?? shortHash),
            _ => null,
        };
    }
}
