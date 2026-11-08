using Gitunia.App;
using Gitunia.Core.FileHistory;
using Gitunia.Tui.App.Reflog;

namespace Gitunia.Tui.App.FileHistory;

/// <summary>
/// Moves the graph cursor to the commit of a file history row. The graph cursor belongs to a controller that has not
/// moved into the state layer, so the shell carries it out and answers with a command: done (the history closes, the
/// graph shows the commit) or refused (the history stays and the status says why).
/// </summary>
/// <param name="host">The dispatcher the answer goes to.</param>
/// <param name="graph">Moves the graph cursor to a commit.</param>
public sealed class FileHistoryJumper(Dispatcher host, IGraphCommitSelector graph)
{
    /// <summary>Selects the commit and dispatches the answer.</summary>
    /// <param name="effect">The tab and the commit.</param>
    public void Jump(FileHistoryJumpEffect effect)
    {
        var refusal = graph.SelectCommit(effect.TabId, effect.Hash);
        if (refusal.Length > 0)
        {
            host.Dispatch(new FileHistoryJumpRefused(refusal));

            return;
        }

        host.Dispatch(new FileHistoryJumpDone());
    }
}
