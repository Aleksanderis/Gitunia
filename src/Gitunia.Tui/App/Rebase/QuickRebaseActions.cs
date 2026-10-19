using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Rebase;

/// <summary>
/// Fixup, reword and drop of the commit selected in the graph (ux.md, workflow 3, "Quick rebase"), run through the
/// interactive rebase engine with a todo built here. Each goes through a Confirm that names the commit, how many later
/// commits get new hashes, the force-push warning and the undo (the old tip, kept in <c>ORIG_HEAD</c> and the
/// reflog); a reword first asks for the new subject in a prompt popup. The refusals are the rebase's (operation in
/// progress, detached HEAD, tracked changes) and none of them runs git. A stop on conflicts is the in-progress rebase
/// of workflow 4. What the action acts on is fixed when it is asked, and the Confirm is dropped when the tab, an
/// overlay or HEAD changed meanwhile.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Plans and runs the rebase.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="refresh">Reads a tab again (refs, graph, status, operation); false when it cannot.</param>
/// <param name="selectedCommit">The hash of the commit selected in the graph, null when none.</param>
/// <param name="isOverlayOpen">True while an overlay is open; a late Confirm must not replace it.</param>
/// <param name="setMode">Switches the keymap mode (Insert while the subject prompt is open).</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with staging, discard, reset and stash.</param>
public sealed class QuickRebaseActions(
    WorkspaceHost host,
    IQuickRebaseWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<TabId, bool> refresh,
    Func<string?> selectedCommit,
    Func<bool> isOverlayOpen,
    Action<KeyMode> setMode,
    Action redraw,
    TabOperationGate gate) : IPromptHandler
{
    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was rebased";
    private const string AskingNotice = "Already asking: answer or cancel the rebase question first";
    private const string SelectNotice = "Select a commit in the graph first";
    private const string ChangedNotice = "The tab or the screen changed while asking: nothing was rebased";

    private readonly HashSet<TabId> _asking = [];
    private QuickRebasePlan? _plan;
    private TabId _promptTab;
    private string _text = string.Empty;

    public string StatusLine => string.Empty;

    public string Notice { get; private set; } = string.Empty;

    public PromptPopup? Popup => _plan is null || host.Workspace.ActiveTabId != _promptTab
        ? null
        : PromptPopup.ForText(
            "Reword commit",
            $"New subject for {RebaseGuard.ShortHash(_plan.Commit)}; the rest of the message is kept",
            _text,
            "next");

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_plan is not null && host.Workspace.ActiveTabId != _promptTab)
        {
            // The prompt belongs to the tab it was opened in; leaving that tab drops it.
            ClosePrompt();
        }

        if (_plan is not null)
        {
            return TryHandleOpenPrompt(action);
        }

        var kind = KindOf(action);
        if (kind is null)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is { } tab)
        {
            Notice = Begin(tab, kind.Value);
        }

        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (_plan is null || host.Workspace.ActiveTabId != _promptTab)
        {
            return false;
        }

        if (PromptTextEditor.Apply(_text, key) is { } edited && edited != _text)
        {
            _text = edited;
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }

    private static QuickRebaseKind? KindOf(ActionId action)
    {
        if (action == ActionIds.RebaseFixup)
        {
            return QuickRebaseKind.Fixup;
        }

        if (action == ActionIds.RebaseReword)
        {
            return QuickRebaseKind.Reword;
        }

        return action == ActionIds.RebaseDrop ? QuickRebaseKind.Drop : null;
    }

    private static string Quoted(QuickRebasePlan plan) =>
        $"{RebaseGuard.ShortHash(plan.Commit)} \"{plan.Subject}\"";

    private static ConfirmPrompt PromptFor(QuickRebaseTarget target, GitRef? current)
    {
        var plan = target.Plan;
        var branch = target.BranchName;
        var (title, first, yes) = plan.Kind switch
        {
            QuickRebaseKind.Fixup => (
                "Fix up commit",
                $"Fix up {Quoted(plan)} into {QuotedCommit(plan.IntoCommit!.Hash, plan.IntoCommit.Subject)}? " +
                "Its changes join that commit, which keeps its message; the fixed-up commit disappears.",
                "Fix up"),
            QuickRebaseKind.Reword => (
                "Reword commit",
                $"Reword {Quoted(plan)} to \"{target.NewSubject}\"? The rest of its message is kept.",
                "Reword"),
            _ => (
                "Drop commit",
                $"Drop {Quoted(plan)} from {branch}? Its changes disappear from the branch.",
                "Drop"),
        };
        List<string> lines =
        [
            first,
            plan.LaterCommits == 0
                ? $"It is the tip of {branch}: the branch gets a new tip commit hash."
                : plan.LaterCommits == 1
                    ? $"1 later commit of {branch} is replayed and gets new hashes."
                    : $"{plan.LaterCommits} later commits of {branch} are replayed and get new hashes.",
        ];
        if (RebaseGuard.UpstreamWarning(current) is { } warning)
        {
            lines.Add(warning);
        }

        return new ConfirmPrompt(
            title,
            lines,
            $"Undo: git reset --hard {plan.Head} (also ORIG_HEAD, the reflog). A stop on conflicts leaves a " +
            "rebase in progress: <space>gc continues, <space>ga aborts.",
            yes);
    }

    private static string QuotedCommit(string hash, string subject) => $"{RebaseGuard.ShortHash(hash)} \"{subject}\"";

    private static string VerbOf(QuickRebaseKind kind) => kind switch
    {
        QuickRebaseKind.Fixup => "fix up",
        QuickRebaseKind.Reword => "reword",
        _ => "drop",
    };

    private static OperationReport ReportOf(QuickRebaseTarget target, InteractiveRebaseResult result)
    {
        var plan = target.Plan;
        var (done, noun) = plan.Kind switch
        {
            QuickRebaseKind.Fixup => ("Fixed up", "Fixup"),
            QuickRebaseKind.Reword => ("Reworded", "Reword"),
            _ => ("Dropped", "Drop"),
        };
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new OperationReport(
                $"{noun} of {Quoted(plan)} stopped on conflicts: resolve the files marked !, then " +
                $"<space>gc (continue), <space>ga (abort, back to {RebaseGuard.ShortHash(result.OriginalHead)})",
                StatusSeverity.Warning);
        }

        return new OperationReport(
            $"{done} {Quoted(plan)} on {target.BranchName} ({RebaseGuard.ShortHash(result.Head ?? string.Empty)}), " +
            $"was {RebaseGuard.ShortHash(result.OriginalHead)}; undo: git reset --hard {result.OriginalHead} " +
            "(or ORIG_HEAD, the reflog)",
            StatusSeverity.Info);
    }

    private bool TryHandleOpenPrompt(ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            Notice = AcceptSubject();
        }
        else if (action == ActionIds.Cancel)
        {
            ClosePrompt();
        }
        else
        {
            return false;
        }

        redraw();
        return true;
    }

    private void ClosePrompt()
    {
        _plan = null;
        setMode(KeyMode.Normal);
    }

    private string AcceptSubject()
    {
        if (_plan is not { } plan || host.Workspace.FindTab(_promptTab) is not { } tab)
        {
            ClosePrompt();
            return string.Empty;
        }

        var subject = _text.Trim();
        if (subject.Length == 0)
        {
            return "The subject is empty: type one or Esc to cancel";
        }

        if (string.Equals(subject, plan.Subject, StringComparison.Ordinal))
        {
            return "The subject is unchanged: edit it or Esc to cancel";
        }

        ClosePrompt();
        Ask(tab.Id, plan, subject);
        return string.Empty;
    }

    private string Begin(RepositoryState tab, QuickRebaseKind kind)
    {
        if (selectedCommit() is not { } hash)
        {
            return SelectNotice;
        }

        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        if (RebaseGuard.Refusal(tab, null) is { } refusal)
        {
            return refusal;
        }

        if (!_asking.Add(tab.Id))
        {
            return AskingNotice;
        }

        var location = tab.Location;
        var token = cancellationOf(tab.Id);
        var tabId = tab.Id;
        _ = Task.Run(() => PlanAsync(tabId, location, kind, hash, token), CancellationToken.None);
        return string.Empty;
    }

    // The plan is a few git calls, so the question opens when it is answered; the state is checked again then.
    private async Task PlanAsync(
        TabId tabId,
        RepositoryLocation location,
        QuickRebaseKind kind,
        string hash,
        CancellationToken token)
    {
        try
        {
            var plan = await writer.PlanAsync(location, kind, hash, token).ConfigureAwait(false);
            postToUi(() => Planned(tabId, plan));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => _asking.Remove(tabId));
        }
        catch (Exception exception)
        {
            var reason = OperationText.Reason(exception);
            postToUi(() =>
            {
                _asking.Remove(tabId);
                statuses.Error($"Could not {VerbOf(kind)} {RebaseGuard.ShortHash(hash)}: {reason}");
            });
        }
    }

    private void Planned(TabId tabId, QuickRebasePlan plan)
    {
        _asking.Remove(tabId);
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        if (!StillValid(tab, plan))
        {
            statuses.Warning(ChangedNotice);
            return;
        }

        if (plan.Kind != QuickRebaseKind.Reword)
        {
            Ask(tabId, plan, null);
            return;
        }

        _plan = plan;
        _promptTab = tabId;
        _text = plan.Subject;
        setMode(KeyMode.Insert);
        redraw();
    }

    private bool StillValid(RepositoryState tab, QuickRebasePlan plan) =>
        !gate.IsBusy(tab.Id)
        && RebaseGuard.Refusal(tab, null) is null
        && string.Equals(tab.Head?.CommitHash, plan.Head, StringComparison.Ordinal)
        && !isOverlayOpen();

    private void Ask(TabId tabId, QuickRebasePlan plan, string? newSubject)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        if (!StillValid(tab, plan))
        {
            statuses.Warning(ChangedNotice);
            return;
        }

        var target = new QuickRebaseTarget(tabId, plan, tab.Head!.BranchName!, newSubject);
        var current = tab.Refs.FirstOrDefault(
            candidate => candidate.IsCurrent && candidate.Kind == RefKind.LocalBranch);
        if (!confirm(PromptFor(target, current), () => Start(target)))
        {
            statuses.Warning(TooSmallNotice);
        }
    }

    private void Start(QuickRebaseTarget target)
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

        if (RebaseGuard.Refusal(tab, null) is { } refusal)
        {
            statuses.Warning(refusal);
            return;
        }

        if (!string.Equals(tab.Head?.CommitHash, target.Plan.Head, StringComparison.Ordinal))
        {
            statuses.Warning(ChangedNotice);
            return;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab, target, token), CancellationToken.None);
    }

    private async Task RunAsync(RepositoryState tab, QuickRebaseTarget target, CancellationToken token)
    {
        try
        {
            var result = await writer.RunAsync(tab.Location, target.Plan, target.NewSubject, token)
                .ConfigureAwait(false);
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
            var failure = $"Could not {VerbOf(target.Plan.Kind)} {Quoted(target.Plan)}: {reason}";
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
