using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Discard;
using Gitunia.Git.Execution;
using Gitunia.Git.Staging.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Discards unstaged changes (ux.md, workflow 2, "Discard"): a file from the Unstaged list, or the hunk or Visual lines of its diff.
/// Nothing is undoable, so nothing runs before the user answers <c>y</c> in a confirm dialog that names what is lost (and says that
/// untracked files are deleted); there is no stash copy. What will be discarded is fixed when the dialog opens, from the cursor at
/// that time, and the git call runs off the UI thread afterwards. When it is done the tab re-reads its status. One operation per
/// tab at a time, shared with staging.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="patchDiscarder">Discards whole files and chosen lines.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="refreshStatus">Asks a tab to re-read its status and calls back when the new state is in; false when it cannot.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when the dialog cannot be shown.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with the staging actions.</param>
public sealed class DiscardActions(
    WorkspaceHost host,
    PatchDiscarder patchDiscarder,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, Action, bool> refreshStatus,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    TabOperationGate gate)
{
    private const string BusyNotice = "Busy: the last stage, unstage or discard is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was discarded";
    private const string DirectorySuffix = "/";
    private const string TrackedConsequence =
        "The changes are removed from the file on disk. Git keeps no copy: this cannot be undone.";
    private const string UntrackedConsequence =
        "It is removed from disk. Git has no copy of it: this cannot be undone.";

    /// <summary>
    /// Asks whether to discard the file under the cursor of the Unstaged list. Returns a notice when no dialog opened, empty
    /// when it did (the discard then waits for the answer).
    /// </summary>
    public string Execute(RepositoryState tab)
    {
        var focus = tab.View.FocusedPane;
        if (focus == PaneKind.WorkingStaged)
        {
            return "Discard works in the Unstaged list: unstage the file first";
        }

        if (focus != PaneKind.WorkingUnstaged)
        {
            return "Focus a file list first";
        }

        if (tab.WorkingTree is not { } tree)
        {
            return "The status is not loaded yet";
        }

        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        var entry = tree.Selection is { } selection && selection.Section == WorkingTreeSection.Unstaged
            ? tree.Unstaged.FirstOrDefault(candidate => candidate.Path == selection.Path)
            : null;
        if (entry is null)
        {
            return "Select a file first";
        }

        var refusal = Refusal(entry);
        if (refusal.Length > 0)
        {
            return refusal;
        }

        return Ask(FilePrompt(entry), () => StartFile(tab, entry));
    }

    /// <summary>
    /// Asks whether to discard the hunk or the Visual lines of <paramref name="request"/> (the diff of an Unstaged file). Returns a
    /// notice when no dialog opened, empty when it did.
    /// </summary>
    /// <param name="tab">The tab whose diff it is.</param>
    /// <param name="request">The file and the changed lines.</param>
    /// <param name="onConfirmed">Runs when the user answered <c>y</c> and the discard starts (the caller ends its range selection).</param>
    /// <param name="onDiscarded">Runs on the UI thread when lines were discarded, before the status is read again.</param>
    public string ExecuteSelection(
        RepositoryState tab,
        DiffStagingRequest request,
        Action? onConfirmed = null,
        Action? onDiscarded = null)
    {
        var entry = request.Target.Entry;
        if (request.Target.Side != WorkingDiffSide.Unstaged)
        {
            return "Discard works in the Unstaged diff: unstage the changes first";
        }

        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        var refusal = Refusal(entry);
        if (refusal.Length > 0)
        {
            return refusal;
        }

        if (request.IsRange && IsWholeFileOnly(entry))
        {
            var name = WorkingTreeRowRenderer.Printable(entry.Path);
            return $"{name} cannot be split into lines: discard the whole file from the list";
        }

        return Ask(SelectionPrompt(request), () =>
        {
            onConfirmed?.Invoke();
            StartSelection(tab, request, onDiscarded);
        });
    }

    private static bool IsWholeFileOnly(StatusEntry entry) =>
        entry.Kind == StatusEntryKind.Untracked || entry.WorktreeChange == FileChangeKind.Deleted;

    // The cases that never reach the dialog: nothing to discard here, or no safe way to do it.
    private static string Refusal(StatusEntry entry)
    {
        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        if (entry.Kind == StatusEntryKind.Unmerged)
        {
            return $"{name} is conflicted: resolve it first (a conflict is not discarded from here)";
        }

        if (entry.Submodule is not null)
        {
            return $"{name} is a submodule: discard its changes inside it";
        }

        if (entry.Kind == StatusEntryKind.Untracked && GitPathText.HasEscapedBytes(entry.Path))
        {
            return $"{name} has a name that is not valid UTF-8: delete it by hand";
        }

        return string.Empty;
    }

    private static ConfirmPrompt FilePrompt(StatusEntry entry)
    {
        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        if (entry.Kind == StatusEntryKind.Untracked)
        {
            var isDirectory = entry.Path.EndsWith(DirectorySuffix, StringComparison.Ordinal);
            var kind = isDirectory ? "directory" : "file";
            var everything = isDirectory ? " and everything in it" : string.Empty;
            return new ConfirmPrompt(
                $"Delete untracked {kind}",
                [$"Delete the untracked {kind} {name}{everything}?"],
                UntrackedConsequence,
                $"Delete {kind}");
        }

        List<string> lines = [$"Discard all unstaged changes to {name}?"];
        if (entry.WorktreeChange == FileChangeKind.Deleted)
        {
            lines.Add("The deleted file is restored from the index.");
        }

        if (entry.IndexChange != FileChangeKind.Unmodified)
        {
            lines.Add("Its staged changes are kept.");
        }

        return new ConfirmPrompt("Discard changes", lines, TrackedConsequence, "Discard changes");
    }

    private static ConfirmPrompt SelectionPrompt(DiffStagingRequest request)
    {
        var entry = request.Target.Entry;
        if (entry.Kind == StatusEntryKind.Untracked)
        {
            return FilePrompt(entry);
        }

        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        var count = request.Lines.Count;
        if (request.IsRange)
        {
            return new ConfirmPrompt(
                "Discard lines",
                [$"Discard the {OperationText.Plural(count, "selected line")} of {name}?"],
                TrackedConsequence,
                "Discard lines");
        }

        if (NamesWholeFile(request))
        {
            var reason = entry.WorktreeChange == FileChangeKind.Deleted ? "is deleted" : "has no hunks to pick";
            return new ConfirmPrompt(
                "Discard changes",
                [$"{name} {reason}. Discard all unstaged changes to it?"],
                TrackedConsequence,
                "Discard changes");
        }

        return new ConfirmPrompt(
            "Discard hunk",
            [$"Discard this hunk ({OperationText.Plural(count, "line")}) of {name}?"],
            TrackedConsequence,
            "Discard hunk");
    }

    private string Ask(ConfirmPrompt prompt, Action onConfirmed) =>
        confirm(prompt, onConfirmed) ? string.Empty : TooSmallNotice;

    private void StartFile(RepositoryState tab, StatusEntry entry)
    {
        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was discarded");
            return;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab, entry, [], true, null, null, token), CancellationToken.None);
    }

    private void StartSelection(RepositoryState tab, DiffStagingRequest request, Action? onDiscarded)
    {
        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was discarded");
            return;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        var entry = request.Target.Entry;

        // The whole file goes only when the dialog said so; a hunk that cannot be cut is refused, never widened.
        var allowWholeFile = !request.IsRange && NamesWholeFile(request);
        _ = Task.Run(
            () => RunAsync(tab, entry, request.Lines, allowWholeFile, request, onDiscarded, token),
            CancellationToken.None);
    }

    // True when SelectionPrompt worded the dialog as discarding or deleting the whole file.
    private static bool NamesWholeFile(DiffStagingRequest request) =>
        request.Target.Entry.Kind == StatusEntryKind.Untracked
        || request.Lines.Count == 0
        || IsWholeFileOnly(request.Target.Entry);

    private async Task RunAsync(
        RepositoryState tab,
        StatusEntry entry,
        IReadOnlyList<ChangedLine> lines,
        bool allowWholeFile,
        DiffStagingRequest? request,
        Action? onDiscarded,
        CancellationToken token)
    {
        try
        {
            var result = await patchDiscarder
                .DiscardAsync(tab.Location, entry, lines, allowWholeFile, token)
                .ConfigureAwait(false);
            postToUi(() => FinishLines(tab.Id, entry, request, result, onDiscarded));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not discard: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error));
        }
    }

    private void FinishLines(
        TabId tabId,
        StatusEntry entry,
        DiffStagingRequest? request,
        PatchStagingResult result,
        Action? onDiscarded)
    {
        if (result.Outcome is PatchStagingOutcome.Applied or PatchStagingOutcome.WholeFile)
        {
            onDiscarded?.Invoke();
        }

        Finish(tabId, Report(entry, request, result), SeverityOf(result));
    }

    private static string Report(StatusEntry entry, DiffStagingRequest? request, PatchStagingResult result)
    {
        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        var isUntracked = entry.Kind == StatusEntryKind.Untracked;
        return result.Outcome switch
        {
            PatchStagingOutcome.Applied => $"Discarded {Describe(request, result)} of {name}",
            PatchStagingOutcome.WholeFile when isUntracked => $"Deleted untracked {name}",
            PatchStagingOutcome.WholeFile when request is null || request.Lines.Count == 0 =>
                $"Discarded all changes to {name}",
            PatchStagingOutcome.WholeFile => $"Discarded all changes to {name}: it has no hunks to pick",
            PatchStagingOutcome.NotSplittable =>
                $"{name} cannot be split into hunks or lines (binary, not valid UTF-8 or renamed): nothing was discarded; "
                + "discard the whole file from the list",
            PatchStagingOutcome.FileChanged =>
                $"{name} changed since the diff was read: nothing was discarded, the diff is read again",
            _ => "Nothing to discard in the selection",
        };
    }

    private static string Describe(DiffStagingRequest? request, PatchStagingResult result) =>
        request is { IsRange: true }
            ? OperationText.Plural(result.LineCount, "line")
            : $"hunk ({OperationText.Plural(result.LineCount, "line")})";

    private static StatusSeverity SeverityOf(PatchStagingResult result) =>
        result.Outcome is PatchStagingOutcome.FileChanged or PatchStagingOutcome.NotSplittable
            ? StatusSeverity.Warning
            : StatusSeverity.Info;

    private void Finish(TabId tabId, string message, StatusSeverity severity)
    {
        statuses.Post(message, severity);

        // The state decides what the lists show, so the next action waits for it (an error may have left the files half changed).
        if (host.Workspace.FindTab(tabId) is null || !refreshStatus(tabId, () => gate.End(tabId)))
        {
            gate.End(tabId);
        }
    }
}
