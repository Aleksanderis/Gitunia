using Gitunia.App;
using Gitunia.Core.Reflog;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Reset;

namespace Gitunia.Tui.App.Reflog;

/// <summary>
/// Starts the flow a reflog entry stands for (reset to it, branch from it, check it out detached, select its commit in
/// the graph). These flows are prompts and dialogs of other controllers, so the shell carries them out and answers with
/// a command: started (the list closes) or refused (the list stays and the status says why).
/// </summary>
/// <param name="host">The dispatcher the answer goes to.</param>
/// <param name="reset">The reset flow an entry starts.</param>
/// <param name="branches">The branch actions an entry starts.</param>
/// <param name="graph">Moves the graph cursor to a commit.</param>
public sealed class ReflogEntryStarter(
    Dispatcher host,
    ResetActions reset,
    BranchActions branches,
    IGraphCommitSelector graph)
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
        ReflogEntryKind.CreateBranch => branches.BeginCreate(effect.TabId, effect.Hash, checkout: true),
        _ => branches.CheckoutDetached(effect.TabId, effect.Hash),
    };
}
