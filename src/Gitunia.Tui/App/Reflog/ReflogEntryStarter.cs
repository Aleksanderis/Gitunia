using Gitunia.App;
using Gitunia.Core.Branches;
using Gitunia.Core.Reflog;
using Gitunia.Core.Running;
using Gitunia.Tui.App.Reset;

namespace Gitunia.Tui.App.Reflog;

/// <summary>
/// Starts the flow a reflog entry stands for (reset to it, branch from it, check it out detached, select its commit in
/// the graph). The reset flow and the graph cursor belong to controllers that have not moved, so the shell carries them
/// out and answers with a command: started (the list closes) or refused (the list stays and the status says why). The
/// branch flows are branch commands.
/// </summary>
/// <param name="host">The dispatcher the answer goes to.</param>
/// <param name="reset">The reset flow an entry starts.</param>
/// <param name="graph">Moves the graph cursor to a commit.</param>
public sealed class ReflogEntryStarter(Dispatcher host, ResetActions reset, IGraphCommitSelector graph)
{
    /// <summary>Starts the flow and dispatches the answer.</summary>
    /// <param name="effect">The entry and what to do with it.</param>
    public void Start(ReflogEntryActionEffect effect)
    {
        var refusal = Begin(effect);
        if (refusal.Length > 0)
        {
            host.Dispatch(new ReflogEntryActionRefused(refusal));

            return;
        }

        // Selecting a commit keeps the list open; the others close it so their prompts and dialogs can show.
        if (effect.Kind != ReflogEntryKind.SelectCommit)
        {
            host.Dispatch(new ReflogEntryActionStarted());
        }
    }

    private string Begin(ReflogEntryActionEffect effect) => effect.Kind switch
    {
        ReflogEntryKind.SelectCommit => graph.SelectCommit(effect.TabId, effect.Hash),
        ReflogEntryKind.Reset => reset.Begin(effect.TabId, effect.Hash),
        _ => CheckOut(effect),
    };

    // Both check the entry out, so a refusal that needs no repository call keeps the list open.
    private string CheckOut(ReflogEntryActionEffect effect)
    {
        if (host.Workspace.FindTab(effect.TabId) is not { } tab)
        {
            return string.Empty;
        }

        var isBusy = tab.Running.IsRunning(RunningOperationKind.Branches);
        if (BranchRefusals.ForCheckout(isBusy, tab.Operation) is { } refusal)
        {
            return refusal;
        }

        if (effect.Kind == ReflogEntryKind.CreateBranch)
        {
            host.Dispatch(new BranchCreateAtRequested(effect.TabId, effect.Hash, true));
        }
        else
        {
            host.Dispatch(new BranchDetachRequested(effect.TabId, effect.Hash));
        }

        return string.Empty;
    }
}
