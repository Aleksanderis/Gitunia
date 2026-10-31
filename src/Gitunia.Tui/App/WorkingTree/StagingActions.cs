using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Staging.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Stages and unstages whole files from the working-tree lists, and hunks or lines from its diff (ux-workflows.md, workflow 2, "Stage and unstage"). The git call runs off the UI
/// thread; when it is done the tab re-reads its status, and only then is the next action accepted, so the list the cursor sits in is
/// current and the same key can be repeated (the state keeps the cursor at the same index). One operation per tab at a time.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Changes the index.</param>
/// <param name="patchStager">Stages and unstages the hunk or lines picked in a diff.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="refreshStatus">Asks a tab to re-read its status and calls back when the new state is in; false when it cannot.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="gate">The one-operation-per-tab gate, shared with the discard actions; null gives this instance its own.</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot be shown. Without it a conflicted file is refused.</param>
public sealed class StagingActions(
    WorkspaceHost host,
    IIndexWriter writer,
    PatchStager patchStager,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, Action, bool> refreshStatus,
    Func<TabId, CancellationToken> cancellationOf,
    TabOperationGate? gate = null,
    Func<ConfirmPrompt, Action, bool>? confirm = null)
{
    private const string DirectorySuffix = "/";
    private const string ContinueKeys = "<space>gc";

    private readonly TabOperationGate _gate = gate ?? new TabOperationGate();

    /// <summary>An operation is running or its status re-read is still pending.</summary>
    public bool IsRunning => _gate.IsAnyBusy;

    /// <summary>True for the actions this class runs.</summary>
    public static bool Handles(ActionId action) =>
        action == ActionIds.WorkingTreeStage
        || action == ActionIds.WorkingTreeStageAll
        || action == ActionIds.WorkingTreeUnstage
        || action == ActionIds.WorkingTreeUnstageAll;

    /// <summary>Starts the action for the focused list of <paramref name="tab"/>; returns a notice when nothing was started, empty otherwise.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="action">Stage, unstage, stage all or unstage all.</param>
    /// <param name="range">The Visual range of rows of the focused list: stage and unstage then apply to those files instead of the one under the cursor.</param>
    public string Execute(RepositoryState tab, ActionId action, RowRange? range = null)
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

        if (_gate.IsBusy(tab.Id))
        {
            return "Busy: the last stage or unstage is still running";
        }

        var isAll = action == ActionIds.WorkingTreeStageAll || action == ActionIds.WorkingTreeUnstageAll;
        if (isStage && !isAll && range is null && ConflictedUnderCursor(tree) is { } conflicted)
        {
            return AskResolved(tab, conflicted);
        }

        var plan = PlanFor(tree, section, isAll, range);
        if (plan.Notice.Length > 0)
        {
            return plan.Notice;
        }

        _gate.Begin(tab.Id);
        var isUnborn = tree.Snapshot.Branch.Head.Kind == HeadKind.Unborn;
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab, plan, isStage, isUnborn, null, token), CancellationToken.None);
        return string.Empty;
    }

    /// <summary>
    /// Stages every unstaged file of <paramref name="tab"/> whatever the focus (as <c>&lt;space&gt;A</c>: untracked
    /// included, conflicted skipped) and calls <paramref name="afterStaged"/> on the UI thread once the status shows the
    /// result. Returns a notice when nothing was started, empty otherwise; it does not run after a failure.
    /// </summary>
    public string StageAllThen(RepositoryState tab, Action afterStaged)
    {
        if (tab.WorkingTree is not { } tree)
        {
            return "The status is not loaded yet";
        }

        if (_gate.IsBusy(tab.Id))
        {
            return "Busy: the last stage or unstage is still running";
        }

        var plan = PlanAll(tree, WorkingTreeSection.Unstaged);
        if (plan.Notice.Length > 0)
        {
            return plan.Notice;
        }

        _gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab, plan, true, false, afterStaged, token), CancellationToken.None);
        return string.Empty;
    }

    /// <summary>
    /// Stages (the diff of an Unstaged file) or unstages (one of a Staged file) the hunk or the Visual lines of
    /// <paramref name="request"/>; a file with no hunks goes whole. Returns a notice when nothing was started, empty otherwise.
    /// </summary>
    public string ExecuteSelection(RepositoryState tab, DiffStagingRequest request, Action? onStaged = null)
    {
        if (_gate.IsBusy(tab.Id))
        {
            return "Busy: the last stage or unstage is still running";
        }

        _gate.Begin(tab.Id);
        var isUnborn = tab.WorkingTree?.Snapshot.Branch.Head.Kind == HeadKind.Unborn;
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunSelectionAsync(tab, request, isUnborn, onStaged, token), CancellationToken.None);
        return string.Empty;
    }

    /// <summary>
    /// Stages the conflicted <paramref name="path"/> of <paramref name="tab"/> right after the conflict editor saved it
    /// with no conflict marker left (no Confirm), then reports how many conflicted files are left and moves the cursor
    /// to the first one left. Returns a notice when nothing was started, empty otherwise.
    /// </summary>
    public string StageResolvedFor(RepositoryState tab, string path)
    {
        var entry = tab.WorkingTree?.Unstaged
            .FirstOrDefault(candidate => candidate.Path == path && candidate.Kind == StatusEntryKind.Unmerged);
        var name = WorkingTreeRowRenderer.Printable(path);
        if (entry is null)
        {
            return $"{name} is no longer listed as conflicted: saved, not staged";
        }

        if (_gate.IsBusy(tab.Id))
        {
            return "Busy: the last stage or unstage is still running; the saved file was not staged";
        }

        _gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        var paths = PathsOf(entry, WorkingTreeSection.Unstaged);
        var plan = StagingPlan.For(paths, name, 1, 0) with { IsSavedResolve = true };
        var report = () => ReportResolved(tab.Id, name, path);
        _ = Task.Run(() => RunAsync(tab, plan, true, false, report, token), CancellationToken.None);

        return string.Empty;
    }

    // The status is re-read by now: the conflicted files left are the ones the banner counts.
    private void ReportResolved(TabId tabId, string name, string path)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        var left = tab.WorkingTree?.Unstaged.Count(entry => entry.Kind == StatusEntryKind.Unmerged) ?? 0;
        if (left == 0)
        {
            var next = tab.Operation is null ? "commit it" : $"{ContinueKeys} continue";
            statuses.Post($"Resolved and staged {name} (Staged list): all conflicts resolved, {next}");
            SelectInSection(tab, WorkingTreeSection.Staged, path);

            return;
        }

        statuses.Post($"Resolved and staged {name} (Staged list), {OperationText.Plural(left, "conflict")} left");
        SelectFirstConflicted(tab);
    }

    // Lands the cursor on the file's own row, so the user sees where the resolved file went.
    private void SelectInSection(RepositoryState tab, WorkingTreeSection section, string path)
    {
        if (tab.WorkingTree is not { } tree)
        {
            return;
        }

        var entries = tree.EntriesOf(section);
        var rows = tree.RowsOf(section);
        for (var row = 0; row < rows.Count; row++)
        {
            if (!rows[row].IsFolder && entries[rows[row].EntryIndex].Path == path)
            {
                host.Dispatch(new TabCommand(tab.Id, new SelectWorkingTreeFileCommand(section, row)));

                return;
            }
        }
    }

    private void SelectFirstConflicted(RepositoryState tab)
    {
        if (tab.WorkingTree is not { } tree)
        {
            return;
        }

        var rows = tree.RowsOf(WorkingTreeSection.Unstaged);
        for (var row = 0; row < rows.Count; row++)
        {
            if (!rows[row].IsFolder && tree.Unstaged[rows[row].EntryIndex].Kind == StatusEntryKind.Unmerged)
            {
                var select = new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, row);
                host.Dispatch(new TabCommand(tab.Id, select));

                return;
            }
        }
    }

    private static StatusEntry? ConflictedUnderCursor(WorkingTreeState tree)
    {
        if (tree.Selection is not { Section: WorkingTreeSection.Unstaged } selection)
        {
            return null;
        }

        return tree.Unstaged.FirstOrDefault(entry =>
            entry.Path == selection.Path && entry.Kind == StatusEntryKind.Unmerged);
    }

    private static StagingPlan PlanFor(WorkingTreeState tree, WorkingTreeSection section, bool isAll, RowRange? range)
    {
        if (isAll)
        {
            return PlanAll(tree, section);
        }

        if (range is { } rows)
        {
            return PlanRange(tree, section, rows);
        }

        return PlanOne(tree, section);
    }

    // Marking a conflicted file resolved is an `add`: the markers stay in the file, so the user confirms first (ux-workflows.md, workflow 2).
    private string AskResolved(RepositoryState tab, StatusEntry entry)
    {
        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        var prompt = new ConfirmPrompt(
            "Mark conflict resolved",
            [
                $"Mark {name} as resolved?",
                "Conflict markers still in the file are not removed or checked: they are committed as they are.",
            ],
            "Undo: git checkout --merge <file> re-creates the conflict (it overwrites the file's current content).",
            "Mark resolved");
        if (confirm is null || !confirm(prompt, () => StartResolved(tab, entry)))
        {
            if (confirm is null)
            {
                return $"{name} is conflicted: resolve it first";
            }

            return "The screen is too small to ask: nothing was marked resolved";
        }

        return string.Empty;
    }

    private void StartResolved(RepositoryState tab, StatusEntry entry)
    {
        if (_gate.IsBusy(tab.Id))
        {
            statuses.Warning("Busy: the last stage or unstage is still running; nothing was marked resolved");
            return;
        }

        _gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        var plan = StagingPlan.For(PathsOf(entry, WorkingTreeSection.Unstaged), name, 1, 0) with { IsResolve = true };
        _ = Task.Run(() => RunAsync(tab, plan, true, false, null, token), CancellationToken.None);
    }

    private static StagingPlan PlanOne(WorkingTreeState tree, WorkingTreeSection section)
    {
        var entries = section == WorkingTreeSection.Staged ? tree.Staged : tree.Unstaged;
        if (tree.Selection is not { } selection || selection.Section != section)
        {
            return StagingPlan.Refused("Select a file first");
        }

        if (selection.IsFolder)
        {
            return PlanFolder(tree, section, selection.Path);
        }

        var entry = entries.FirstOrDefault(candidate => candidate.Path == selection.Path);
        if (entry is null)
        {
            return StagingPlan.Refused("Select a file first");
        }

        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        if (entry.Kind == StatusEntryKind.Unmerged)
        {
            return StagingPlan.Refused($"{name} is conflicted: mark it resolved from the Unstaged list");
        }

        return StagingPlan.For(PathsOf(entry, section), name, 1, 0);
    }

    // Every file under the folder, wherever it sits in the list; conflicted ones are skipped like in stage all.
    private static StagingPlan PlanFolder(WorkingTreeState tree, WorkingTreeSection section, string folder)
    {
        var entries = tree.EntriesOf(section);
        var picked = tree.RowsOf(section).EntriesUnder(folder).Select(index => entries[index]).ToList();
        var name = $"{WorkingTreeRowRenderer.Printable(folder)}{DirectorySuffix}";
        if (picked.Count == 0)
        {
            return StagingPlan.Refused($"{name} holds no files");
        }

        return PlanOf(picked, section, name);
    }

    // The files of the Visual range (a folder row stands for all the files under it), once each; conflicted
    // ones are skipped like in stage all.
    private static StagingPlan PlanRange(WorkingTreeState tree, WorkingTreeSection section, RowRange range)
    {
        var entries = tree.EntriesOf(section);
        var rows = tree.RowsOf(section);
        if (rows.Count == 0)
        {
            return StagingPlan.Refused("Select a file first");
        }

        var first = Math.Clamp(range.First, 0, rows.Count - 1);
        var last = Math.Clamp(range.Last, first, rows.Count - 1);
        var picked = rows.EntriesOfRows(first, last).Select(index => entries[index]).ToList();

        return PlanOf(picked, section, null);
    }

    private static StagingPlan PlanOf(List<StatusEntry> picked, WorkingTreeSection section, string? folderName)
    {
        var usable = picked.Where(entry => entry.Kind != StatusEntryKind.Unmerged).ToList();
        var skipped = picked.Count - usable.Count;
        if (usable.Count == 0)
        {
            return StagingPlan.Refused($"Only conflicted files are selected ({skipped} skipped; mark one resolved on its own)");
        }

        var name = string.Empty;
        if (folderName is not null)
        {
            name = $"{folderName} ({OperationText.Plural(usable.Count, "file")})";
        }
        else if (usable.Count == 1)
        {
            name = WorkingTreeRowRenderer.Printable(usable[0].Path);
        }

        return StagingPlan.For([.. usable.SelectMany(entry => PathsOf(entry, section))], name, usable.Count, skipped);
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

    private static IReadOnlyList<string> PathsOf(StatusEntry entry, WorkingTreeSection section) =>
        StagingPaths.Of(entry, section == WorkingTreeSection.Staged ? WorkingDiffSide.Staged : WorkingDiffSide.Unstaged);

    private async Task RunAsync(
        RepositoryState tab,
        StagingPlan plan,
        bool isStage,
        bool isUnborn,
        Action? afterStaged,
        CancellationToken token)
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

            postToUi(() => Finish(tab.Id, Report(plan, isStage), StatusSeverity.Info, afterStaged));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Release(tab.Id));
        }
        catch (Exception exception)
        {
            var verb = isStage ? "stage" : "unstage";
            postToUi(() => Finish(tab.Id, $"Could not {verb}: {OperationText.Reason(exception)}", StatusSeverity.Error));
        }
    }

    private async Task RunSelectionAsync(
        RepositoryState tab,
        DiffStagingRequest request,
        bool isUnborn,
        Action? onStaged,
        CancellationToken token)
    {
        var isStage = request.Target.Side == WorkingDiffSide.Unstaged;
        try
        {
            // Lines picked one by one are never turned into a whole-file stage behind the user's back.
            var result = await patchStager
                .StageAsync(
                    tab.Location,
                    request.Target.Side,
                    request.Target.Entry,
                    request.Lines,
                    !request.IsRange,
                    isUnborn,
                    token)
                .ConfigureAwait(false);
            postToUi(() => FinishSelection(tab.Id, request, result, onStaged));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Release(tab.Id));
        }
        catch (Exception exception)
        {
            var verb = isStage ? "stage" : "unstage";
            postToUi(() => Finish(tab.Id, $"Could not {verb} the selection: {OperationText.Reason(exception)}", StatusSeverity.Error));
        }
    }

    private void FinishSelection(TabId tabId, DiffStagingRequest request, PatchStagingResult result, Action? onStaged)
    {
        if (result.Outcome is PatchStagingOutcome.Applied or PatchStagingOutcome.WholeFile)
        {
            onStaged?.Invoke();
        }

        Finish(tabId, Report(request, result), SeverityOf(result));
    }

    private static string Report(DiffStagingRequest request, PatchStagingResult result)
    {
        var isStage = request.Target.Side == WorkingDiffSide.Unstaged;
        var verb = isStage ? "stage" : "unstage";
        var name = WorkingTreeRowRenderer.Printable(request.Target.Entry.Path);
        var what = request.IsRange ? OperationText.Plural(result.LineCount, "line") : $"change ({OperationText.Plural(result.LineCount, "line")})";
        return result.Outcome switch
        {
            PatchStagingOutcome.Applied => $"{(isStage ? "Staged" : "Unstaged")} {what} of {name}",
            PatchStagingOutcome.WholeFile =>
                $"{(isStage ? "Staged" : "Unstaged")} {name} whole: new, deleted and binary files are not split into hunks",
            PatchStagingOutcome.NotSplittable =>
                $"{name} cannot be split into lines (new, deleted or binary file): {verb} its change or the whole file",
            PatchStagingOutcome.FileChanged => $"{name} changed since the diff was read: nothing was {verb}d, the diff is read again",
            _ => $"Nothing to {verb} in the selection",
        };
    }

    private static StatusSeverity SeverityOf(PatchStagingResult result)
    {
        if (result.Outcome is PatchStagingOutcome.FileChanged or PatchStagingOutcome.NotSplittable)
        {
            return StatusSeverity.Warning;
        }

        return StatusSeverity.Info;
    }

    private void Finish(TabId tabId, string message, StatusSeverity severity, Action? afterStaged = null)
    {
        statuses.Post(message, severity);

        // The state decides what the lists show, so the next action waits for it (an error may have left the index half changed).
        if (host.Workspace.FindTab(tabId) is null || !refreshStatus(tabId, () => Done(tabId, afterStaged)))
        {
            Done(tabId, afterStaged);
        }
    }

    private void Done(TabId tabId, Action? afterStaged)
    {
        Release(tabId);
        afterStaged?.Invoke();
    }

    private void Release(TabId tabId) => _gate.End(tabId);

    private static string Report(StagingPlan plan, bool isStage)
    {
        if (plan.IsSavedResolve)
        {
            return $"Resolved and staged {plan.Name}";
        }

        if (plan.IsResolve)
        {
            return $"Marked {plan.Name} resolved";
        }

        var verb = isStage ? "Staged" : "Unstaged";
        var what = $"{plan.Count} files";
        if (plan.Name.Length > 0)
        {
            what = plan.Name;
        }
        else if (plan.Count == 1)
        {
            what = "1 file";
        }

        return plan.Skipped > 0 ? $"{verb} {what} ({plan.Skipped} conflicted skipped)" : $"{verb} {what}";
    }
}
