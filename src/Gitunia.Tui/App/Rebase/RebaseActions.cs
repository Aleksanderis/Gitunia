using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Rebase;

/// <summary>
/// Rebases the current branch onto the selected commit, branch or tag (ux.md, workflow 3, "Integrating"). A Confirm
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

    private const int ShortHashLength = 7;
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

    /// <summary>
    /// The warning for a branch that has an upstream (a rebase rewrites its history and needs a force push), stronger
    /// when every commit is already on it; null without an upstream. Shared with the interactive rebase's Confirm.
    /// </summary>
    public static string? UpstreamWarning(GitRef? current)
    {
        if (current is not { UpstreamFullName: { } upstream, IsUpstreamGone: false })
        {
            return null;
        }

        var pushed = current.Ahead == 0 ? "All of its commits are already on" : "It tracks";
        return $"{pushed} {ShortRef(upstream)}: the history is rewritten and a force push is needed afterwards.";
    }

    /// <summary>
    /// Why a rebase must not start in this tab, or null: an operation in progress, a detached or unborn HEAD, the
    /// target being the current commit, or tracked changes. No git runs for any of these; the interactive rebase
    /// uses the same.
    /// </summary>
    public static string? Refusal(RepositoryState tab, string targetHash)
    {
        if (tab.Operation is { } info)
        {
            var name = OperationBannerBuilder.NameOf(info.Kind);
            return $"A {name} is in progress: finish or abort the {name} first (<space>gc continue, <space>ga abort)";
        }

        if (tab.Head is not { Kind: HeadKind.Branch, BranchName: not null, CommitHash: not null })
        {
            return tab.Head?.Kind switch
            {
                null => "The repository is still loading: try again in a moment",
                HeadKind.Unborn => "The branch has no commits yet: nothing to rebase",
                _ => "HEAD is detached: switch to a branch first",
            };
        }

        if (string.Equals(tab.Head.CommitHash, targetHash, StringComparison.Ordinal))
        {
            return $"{tab.Head.BranchName} is already at that commit: nothing to rebase";
        }

        var changed = tab.WorkingTree?.Snapshot.Entries.Count(IsTrackedChange) ?? 0;
        return changed == 0
            ? null
            : $"{OperationText.Plural(changed, "file")} with uncommitted changes: commit or stash them before a rebase";
    }

    private static string ShortHash(string hash) => hash[..Math.Min(ShortHashLength, hash.Length)];

    private static bool IsTrackedChange(StatusEntry entry) =>
        entry.Kind is not (StatusEntryKind.Untracked or StatusEntryKind.Ignored);

    private static string ShortRef(string fullName) =>
        fullName.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal)
            ? fullName[GitRefPrefixes.Remotes.Length..]
            : fullName.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal)
                ? fullName[GitRefPrefixes.Heads.Length..]
                : fullName;

    private static ConfirmPrompt PromptFor(RebaseTarget target, GitRef? current, int merges)
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
                "itself is dropped and the commits are replayed as one line.");
        }

        if (UpstreamWarning(current) is { } warning)
        {
            lines.Add(warning);
        }

        return new ConfirmPrompt(
            "Rebase",
            lines,
            $"Undo: git reset --hard {target.OldTip} (also ORIG_HEAD, the reflog). A stop on conflicts leaves a " +
            "rebase in progress: <space>gc continues, <space>ga aborts.",
            "Rebase");
    }

    private static OperationReport ReportOf(RebaseTarget target, OperationResult result)
    {
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new OperationReport(
                $"Rebase of {target.BranchName} onto {target.Label} stopped on conflicts: resolve the files " +
                $"marked !, then <space>gc (continue), <space>ga (abort, back to {ShortHash(target.OldTip)})",
                StatusSeverity.Warning);
        }

        if (string.Equals(result.Head, target.OldTip, StringComparison.Ordinal))
        {
            return new OperationReport(
                $"{target.BranchName} is already based on {target.Label}: nothing changed",
                StatusSeverity.Info);
        }

        return new OperationReport(
            $"Rebased {target.BranchName} onto {target.Label} ({ShortHash(result.Head ?? string.Empty)}), " +
            $"was {ShortHash(target.OldTip)}; undo: git reset --hard {target.OldTip} (or ORIG_HEAD, the reflog)",
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

        if (Refusal(tab, hash) is { } refusal)
        {
            return refusal;
        }

        var label = chosen.Name ?? ShortHash(hash);
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
        if (gate.IsBusy(tab.Id) || Refusal(tab, target.CommitHash) is not null || moved || isOverlayOpen())
        {
            statuses.Warning("The tab or the screen changed while asking: nothing was rebased");
            return;
        }

        if (!confirm(PromptFor(target, current, merges), () => Start(target)))
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

        if (Refusal(tab, target.CommitHash) is { } refusal)
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
