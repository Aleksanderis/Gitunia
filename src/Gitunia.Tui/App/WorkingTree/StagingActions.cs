using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Stages and unstages whole files from the working-tree lists (ux.md, workflow 2, "Stage and unstage"). The git call runs off the UI
/// thread; when it is done the tab re-reads its status, and only then is the next action accepted, so the list the cursor sits in is
/// current and the same key can be repeated (the state keeps the cursor at the same index). One operation per tab at a time.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Changes the index.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="refreshStatus">Asks a tab to re-read its status and calls back when the new state is in; false when it cannot.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
public sealed class StagingActions(
    WorkspaceHost host,
    IIndexWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, Action, bool> refreshStatus,
    Func<TabId, CancellationToken> cancellationOf)
{
    private readonly HashSet<TabId> _busy = [];

    /// <summary>An operation is running or its status re-read is still pending.</summary>
    public bool IsRunning => _busy.Count > 0;

    /// <summary>True for the actions this class runs.</summary>
    public static bool Handles(ActionId action) =>
        action == ActionIds.WorkingTreeStage
        || action == ActionIds.WorkingTreeStageAll
        || action == ActionIds.WorkingTreeUnstage
        || action == ActionIds.WorkingTreeUnstageAll;

    /// <summary>Starts the action for the focused list of <paramref name="tab"/>; returns a notice when nothing was started, empty otherwise.</summary>
    public string Execute(RepositoryState tab, ActionId action)
    {
        var isStage = action == ActionIds.WorkingTreeStage || action == ActionIds.WorkingTreeStageAll;
        var section = isStage ? WorkingTreeSection.Unstaged : WorkingTreeSection.Staged;
        var focus = tab.View.FocusedPane;
        if (focus != PaneKind.WorkingUnstaged && focus != PaneKind.WorkingStaged)
        {
            return "Focus a file list first";
        }

        var focused = focus == PaneKind.WorkingStaged ? WorkingTreeSection.Staged : WorkingTreeSection.Unstaged;
        if (focused != section)
        {
            return isStage ? "Stage works in the Unstaged list" : "Unstage works in the Staged list";
        }

        if (tab.WorkingTree is not { } tree)
        {
            return "The status is not loaded yet";
        }

        if (_busy.Contains(tab.Id))
        {
            return "Busy: the last stage or unstage is still running";
        }

        var isAll = action == ActionIds.WorkingTreeStageAll || action == ActionIds.WorkingTreeUnstageAll;
        var plan = isAll ? PlanAll(tree, section) : PlanOne(tree, section);
        if (plan.Notice.Length > 0)
        {
            return plan.Notice;
        }

        _busy.Add(tab.Id);
        var isUnborn = tree.Snapshot.Branch.Head.Kind == HeadKind.Unborn;
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab, plan, isStage, isUnborn, token), CancellationToken.None);
        return string.Empty;
    }

    private static StagingPlan PlanOne(WorkingTreeState tree, WorkingTreeSection section)
    {
        var entries = section == WorkingTreeSection.Staged ? tree.Staged : tree.Unstaged;
        if (tree.Selection is not { } selection || selection.Section != section)
        {
            return StagingPlan.Refused("Select a file first");
        }

        var entry = entries.FirstOrDefault(candidate => candidate.Path == selection.Path);
        if (entry is null)
        {
            return StagingPlan.Refused("Select a file first");
        }

        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        if (entry.Kind == StatusEntryKind.Unmerged)
        {
            return StagingPlan.Refused($"{name} is conflicted: resolve it first (marking it resolved is not built yet)");
        }

        return StagingPlan.For(PathsOf(entry, section), name, 1, 0);
    }

    private static StagingPlan PlanAll(WorkingTreeState tree, WorkingTreeSection section)
    {
        var entries = section == WorkingTreeSection.Staged ? tree.Staged : tree.Unstaged;
        var usable = entries.Where(entry => entry.Kind != StatusEntryKind.Unmerged).ToList();
        var skipped = entries.Count - usable.Count;
        if (usable.Count == 0)
        {
            return StagingPlan.Refused(skipped > 0 ? $"Only conflicted files are left ({skipped} skipped)" : "Nothing to do");
        }

        return StagingPlan.For([.. usable.SelectMany(entry => PathsOf(entry, section))], string.Empty, usable.Count, skipped);
    }

    // Unstaging a rename must reset the old path too, or the deletion of it stays staged.
    private static IReadOnlyList<string> PathsOf(StatusEntry entry, WorkingTreeSection section) =>
        section == WorkingTreeSection.Staged && entry.Kind == StatusEntryKind.Renamed && entry.OriginalPath is { } original
            ? [entry.Path, original]
            : [entry.Path];

    private async Task RunAsync(RepositoryState tab, StagingPlan plan, bool isStage, bool isUnborn, CancellationToken token)
    {
        try
        {
            if (isStage)
            {
                await writer.StageAsync(tab.Location, plan.Paths, token).ConfigureAwait(false);
            }
            else
            {
                await writer.UnstageAsync(tab.Location, plan.Paths, isUnborn, token).ConfigureAwait(false);
            }

            postToUi(() => Finish(tab.Id, Report(plan, isStage), false));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Release(tab.Id));
        }
        catch (Exception exception)
        {
            var verb = isStage ? "stage" : "unstage";
            postToUi(() => Finish(tab.Id, $"Could not {verb}: {Reason(exception)}", true));
        }
    }

    // Git's own first line of stderr; the exception message also repeats the whole command line.
    private static string Reason(Exception exception)
    {
        var text = exception is GitCommandFailedException failed ? failed.StandardError : exception.Message;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[0] : "unknown error";
    }

    private void Finish(TabId tabId, string message, bool isError)
    {
        if (isError)
        {
            statuses.Error(message);
        }
        else
        {
            statuses.Post(message);
        }

        // The state decides what the lists show, so the next action waits for it (an error may have left the index half changed).
        if (host.Workspace.FindTab(tabId) is null || !refreshStatus(tabId, () => Release(tabId)))
        {
            Release(tabId);
        }
    }

    private void Release(TabId tabId) => _busy.Remove(tabId);

    private static string Report(StagingPlan plan, bool isStage)
    {
        var verb = isStage ? "Staged" : "Unstaged";
        var what = plan.Name.Length > 0 ? plan.Name : plan.Count == 1 ? "1 file" : $"{plan.Count} files";
        return plan.Skipped > 0 ? $"{verb} {what} ({plan.Skipped} conflicted skipped)" : $"{verb} {what}";
    }
}
