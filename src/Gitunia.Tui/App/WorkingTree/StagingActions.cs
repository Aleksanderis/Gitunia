using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Running;
using Gitunia.Core.Staging;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Turns the staging keys of the Changes view and the two requests of other controllers (stage all before a commit, stage
/// a file the conflict editor resolved) into staging commands. Everything that decides (what the key touches, the busy
/// checks, the dialog that marks a conflict resolved, the status text) is the Core staging reducer; the git work is its
/// effect (ux-workflows.md, workflow 2, "Stage and unstage").
/// </summary>
/// <param name="host">The workspace the commands go to.</param>
public sealed class StagingActions(Dispatcher host)
{
    /// <summary>True for the actions this class runs.</summary>
    public static bool Handles(ActionId action) =>
        action == ActionIds.WorkingTreeStage
        || action == ActionIds.WorkingTreeStageAll
        || action == ActionIds.WorkingTreeUnstage
        || action == ActionIds.WorkingTreeUnstageAll;

    /// <summary>A stage or unstage runs in the tab, or its status is still being read again.</summary>
    public bool IsRunning(TabId tabId) =>
        host.Workspace.FindTab(tabId)?.Running.IsRunning(RunningOperationKind.Staging) == true;

    /// <summary>
    /// Sends the key for the focused list of <paramref name="tab"/>. Returns a notice when other work holds the tab, empty
    /// otherwise; the reducer posts its own refusals (use <see cref="IsRunning"/> to see whether something started).
    /// </summary>
    /// <param name="tab">The tab.</param>
    /// <param name="action">Stage, unstage, stage all or unstage all.</param>
    /// <param name="range">The Visual range of rows of the focused list: stage and unstage then apply to those files
    /// instead of the one under the cursor.</param>
    public string Execute(RepositoryState tab, ActionId action, RowRange? range = null)
    {
        Send(new StagingRequested(tab.Id, ActionOf(action), range));

        return string.Empty;
    }

    /// <summary>
    /// Stages (the diff of an Unstaged file) or unstages (one of a Staged file) the hunk or the Visual lines of
    /// <paramref name="request"/>; a file with no hunks goes whole. The cursor of the diff goes to <paramref name="move"/>
    /// once the diff is read again.
    /// </summary>
    public string ExecuteSelection(RepositoryState tab, DiffStagingRequest request, DiffCursorMove move)
    {
        Send(new StageSelectionRequested(tab.Id, request.ToSelection(), move));

        return string.Empty;
    }

    /// <summary>
    /// Stages the conflicted <paramref name="path"/> of <paramref name="tab"/> right after the conflict editor saved it
    /// with no conflict marker left (no dialog), then reports how many conflicted files are left. A refusal is posted by
    /// the reducer, which also reads the status again.
    /// </summary>
    public string StageResolvedFor(RepositoryState tab, string path)
    {
        Send(new StageResolvedRequested(tab.Id, path));

        return string.Empty;
    }

    private static StagingAction ActionOf(ActionId action)
    {
        if (action == ActionIds.WorkingTreeStage)
        {
            return StagingAction.Stage;
        }

        if (action == ActionIds.WorkingTreeStageAll)
        {
            return StagingAction.StageAll;
        }

        return action == ActionIds.WorkingTreeUnstage ? StagingAction.Unstage : StagingAction.UnstageAll;
    }

    private void Send(StagingCommand command) => host.Dispatch(command);
}
