using Gitunia.Core.Conflicts;
using Gitunia.App;
using Gitunia.App.Status;
using Gitunia.Core.FileTree;
using Gitunia.Core.Reflog;
using Gitunia.Core.Shell;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Shell;

/// <summary>The shell effects that continue a flow in a view: a reflog entry, a resolved conflict, a file's content.</summary>
/// <param name="reflog">Starts the flow a reflog entry stands for.</param>
/// <param name="staging">Marks a conflicted file resolved.</param>
/// <param name="host">Takes the refresh request.</param>
/// <param name="statuses">Reports the outcome.</param>
/// <param name="diffPane">Shows a file's content in the diff pane.</param>
public sealed class FlowEffects(
    ReflogEntryStarter reflog,
    StagingActions staging,
    Dispatcher host,
    StatusReporter statuses,
    DiffPaneInput diffPane) : IFlowEffects
{
    /// <summary>Starts the flow a reflog entry stands for.</summary>
    /// <param name="effect">The entry and the flow.</param>
    public void StartReflogEntryAction(ReflogEntryActionEffect effect) => reflog.Start(effect);

    /// <summary>A saved conflicted file with no conflict left is marked resolved; the editor closed already.</summary>
    /// <param name="effect">The tab and the file.</param>
    public void StageResolvedConflict(StageResolvedConflictEffect effect)
    {
        if (host.Workspace.FindTab(effect.TabId) is not { } tab)
        {
            return;
        }

        var notice = staging.StageResolvedFor(tab, effect.Path);
        if (notice.Length > 0)
        {
            host.Dispatch(new TabRefreshRequested(effect.TabId, false));
            statuses.Warning(notice);
        }
    }

    /// <summary>Shows the content of a file row of the Files tab in the diff pane's place.</summary>
    /// <param name="row">The file row.</param>
    public void OpenFileContent(FileTreeRow row) => diffPane.OpenFileContent(row);
}
