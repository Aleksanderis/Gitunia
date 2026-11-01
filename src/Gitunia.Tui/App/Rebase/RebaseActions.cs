using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Rebase;

/// <summary>
/// Rebases the current branch onto the selected commit, branch or tag (ux-workflows.md, workflow 3, "Integrating"). A Confirm
/// names both sides, warns that a branch with an upstream needs a force push afterwards and shows the undo (the old
/// tip, kept in <c>ORIG_HEAD</c> and the reflog). Refused without running git on a detached or unborn HEAD, while an
/// operation is in progress and when tracked files have changes. A rebase that stops on conflicts is not a failure: the
/// operation banner and continue, abort and skip of workflow 4 take over. Non-interactive only (the interactive
/// engine is IInteractiveRebaseWriter, #399).
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs the rebase.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="refresh">Reads a tab again (refs, graph, status, operation); false when it cannot.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
/// <param name="isOverlayOpen">True while an overlay is open; a late Confirm must not replace it.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with staging, discard, reset and stash.</param>
public sealed class RebaseActions(
    WorkspaceHost host,
    IRebaseWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<TabId, bool> refresh,
    Func<BranchSelection> selection,
    Func<bool> isOverlayOpen,
    TabOperationGate gate)
{
    private readonly HashSet<TabId> _asking = [];

    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was rebased";
    private const string AskingNotice = "Already asking: answer or cancel the rebase question first";
    private const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) to rebase onto";

    /// <summary>The notice for the status bar after the last <see cref="TryExecute"/>, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action != ActionIds.RebaseOnto)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is { } tab)
        {
            Notice = Begin(tab, selection());
        }

        return true;
    }

    private static ConfirmPrompt PromptFor(RebaseTarget target, GitRef? current, int merges, string? autoStashNote)
    {
        List<string> lines =
        [
            $"Rebase {target.BranchName} onto {target.Label}?",
            $"Its commits that {target.Label} does not have are replayed on top of {target.Label}; " +
            "they get new hashes.",
        ];
        if (merges > 0)
        {
            lines.Add(
                $"{OperationText.Plural(merges, "merge commit")} in that range will be flattened: the merge " +
                "itself is dropped and the commits are replayed as one line. <space>bri keeps merges " +
                "(interactive rebase, --rebase-merges).");
        }

        if (RebaseGuard.UpstreamWarning(current) is { } warning)
        {
            lines.Add(warning);
        }

        if (autoStashNote is not null)
        {
            lines.Add(autoStashNote);
        }

        return new ConfirmPrompt(
            "Rebase",
            lines,
            $"Undo: git reset --keep {target.OldTip} (also ORIG_HEAD, the reflog). A stop on conflicts leaves a " +
            "rebase in progress: <space>gc continues, <space>ga aborts.",
            "Rebase");
    }

    private static OperationReport ReportOf(RebaseTarget target, OperationResult result)
    {
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new OperationReport(
                $"Rebase of {target.BranchName} onto {target.Label} stopped on conflicts: resolve the files " +
                $"marked !, then <space>gc (continue), <space>ga (abort, back to " +
                $"{ShortHash.Of(target.OldTip)})",
                StatusSeverity.Warning);
        }

        if (string.Equals(result.Head, target.OldTip, StringComparison.Ordinal))
        {
            return new OperationReport(
                $"{target.BranchName} is already based on {target.Label}: nothing changed",
                StatusSeverity.Info);
        }

        var head = ShortHash.Of(result.Head);
        var was = ShortHash.Of(target.OldTip);
        return new OperationReport(
            $"Rebased {target.BranchName} onto {target.Label} ({head}), was {was}; " +
            $"undo: git reset --keep {target.OldTip} (or ORIG_HEAD, the reflog)",
            StatusSeverity.Info);
    }

    private string Begin(RepositoryState tab, BranchSelection chosen)
    {
        var isRebaseTarget = chosen.RowKind is null or SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch
            or SidebarRowKind.Tag;
        if (!isRebaseTarget || chosen.CommitHash is not { } hash)
        {
            return SelectNotice;
        }

        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        if (RebaseGuard.Refusal(tab, hash) is { } refusal)
        {
            return refusal;
        }

        var label = chosen.Name ?? ShortHash.Of(hash);
        var target = new RebaseTarget(tab.Id, hash, label, tab.Head!.BranchName!, tab.Head.CommitHash!);
        var current = tab.Refs.FirstOrDefault(
            candidate => candidate.IsCurrent && candidate.Kind == RefKind.LocalBranch);
        if (!_asking.Add(tab.Id))
        {
            return AskingNotice;
        }

        var location = tab.Location;
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => AskAsync(location, target, current, token), CancellationToken.None);
        return string.Empty;
    }

    // The merge count is a git call, so the Confirm opens when it is answered; the state is checked again then.
    private async Task AskAsync(
        RepositoryLocation location,
        RebaseTarget target,
        GitRef? current,
        CancellationToken token)
    {
        int merges;
        try
        {
            merges = await writer.CountMergesAsync(location, target.CommitHash, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => _asking.Remove(target.TabId));
            return;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The count only decides on a warning; the rebase itself reports git's reason if the range is bad.
            postToUi(() => Ask(target, current, 0));
            return;
        }

        postToUi(() => Ask(target, current, merges));
    }

    private void Ask(RebaseTarget target, GitRef? current, int merges)
    {
        if (host.Workspace.FindTab(target.TabId) is not { } tab)
        {
            return;
        }

        _asking.Remove(tab.Id);
        var moved = !string.Equals(tab.Head?.CommitHash, target.OldTip, StringComparison.Ordinal);
        if (gate.IsBusy(tab.Id) || RebaseGuard.Refusal(tab, target.CommitHash) is not null || moved || isOverlayOpen())
        {
            statuses.Warning("The tab or the screen changed while asking: nothing was rebased");
            return;
        }

        if (!confirm(PromptFor(target, current, merges, RebaseGuard.AutoStashNote(tab)), () => Start(target)))
        {
            statuses.Warning(TooSmallNotice);
        }
    }

    private void Start(RebaseTarget target)
    {
        if (host.Workspace.FindTab(target.TabId) is not { } tab)
        {
            return;
        }

        // The dialog can outlive the state it was opened in.
        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was rebased");
            return;
        }

        if (RebaseGuard.Refusal(tab, target.CommitHash) is { } refusal)
        {
            statuses.Warning(refusal);
            return;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RebaseAsync(tab, target, token), CancellationToken.None);
    }

    private async Task RebaseAsync(RepositoryState tab, RebaseTarget target, CancellationToken token)
    {
        try
        {
            var result = await writer.RebaseAsync(tab.Location, target.CommitHash, token).ConfigureAwait(false);
            var report = ReportOf(target, result);
            postToUi(() => Finish(tab.Id, report.Text, report.Severity));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception)
        {
            var reason = OperationText.Reason(exception);
            var failure = $"Could not rebase {target.BranchName} onto {target.Label}: {reason}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error));
        }
    }

    private void Finish(TabId tabId, string message, StatusSeverity severity)
    {
        statuses.Post(message, severity);
        gate.End(tabId);
        if (host.Workspace.FindTab(tabId) is not null)
        {
            refresh(tabId);
        }
    }
}
