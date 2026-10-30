using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Operations;

/// <summary>
/// Continue, abort and skip for the merge, rebase, cherry-pick or revert that is stopped in the active tab (ux-workflows.md,
/// workflow 4).
/// Continue is refused while conflicted files remain; abort and skip ask first and name what is undone or dropped.
/// What is acted
/// on (the operation and its commit) is fixed when the action starts, so a refresh while the dialog is open never
/// redirects it.
/// The git call runs off the UI thread; afterwards the tab reads everything again, which updates the banner. One
/// operation per tab
/// at a time, shared with staging.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs the git calls.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="refresh">Asks a tab to read everything again; false when it cannot.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog, runs the callback on <c>y</c>; false if it cannot show.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with the staging actions.</param>
public sealed class OperationController(
    WorkspaceHost host,
    IOperationWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, bool> refresh,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    TabOperationGate gate)
{
    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";

    /// <summary>The notice for the status bar after the last <see cref="TryExecute"/>, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action != ActionIds.OperationContinue && action != ActionIds.OperationAbort
            && action != ActionIds.OperationSkip)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is not { } tab)
        {
            return true;
        }

        Notice = gate.IsBusy(tab.Id) ? BusyNotice : Start(tab, action);
        return true;
    }

    private string Start(RepositoryState tab, ActionId action)
    {
        if (tab.Operation is not { } info)
        {
            return OperationTexts.NoOperationNotice;
        }

        var branch = tab.Head?.BranchName;
        if (action == ActionIds.OperationContinue)
        {
            return StartContinue(tab, info, branch);
        }

        if (action == ActionIds.OperationAbort)
        {
            var oldTip = info.OriginalHead ?? tab.Head?.CommitHash;
            var prompt = OperationTexts.AbortPrompt(info, branch, oldTip);
            var asked = confirm(prompt, () => StartAbort(tab.Id, tab.Location, info, branch));
            return asked ? string.Empty : TooSmallNotice;
        }

        if (info.Kind == RepositoryOperation.Merge)
        {
            return OperationTexts.CannotSkipMerge;
        }

        if (confirm(OperationTexts.SkipPrompt(info), () => StartSkip(tab.Id, tab.Location, info)))
        {
            return string.Empty;
        }

        return TooSmallNotice;
    }

    private string StartContinue(RepositoryState tab, OperationInfo info, string? branch)
    {
        var conflicts = OperationBannerBuilder.ConflictCount(tab.WorkingTree);
        if (conflicts > 0)
        {
            return OperationTexts.StillConflicted(conflicts);
        }

        Begin(
            tab.Id,
            "continue",
            info,
            token => writer.ContinueAsync(tab.Location, info.Kind, token),
            result => OperationTexts.ContinueReport(info, branch, result));
        return string.Empty;
    }

    private void StartAbort(TabId tabId, RepositoryLocation location, OperationInfo info, string? branch)
    {
        if (!CanBegin(tabId, "aborted"))
        {
            return;
        }

        Begin(
            tabId,
            "abort",
            info,
            token => writer.AbortAsync(location, info.Kind, token),
            result => OperationTexts.AbortReport(info, branch, result));
    }

    private void StartSkip(TabId tabId, RepositoryLocation location, OperationInfo info)
    {
        if (!CanBegin(tabId, "skipped"))
        {
            return;
        }

        if (host.Workspace.FindTab(tabId)?.Operation is not { } current
            || current.Kind != info.Kind
            || current.Commit != info.Commit)
        {
            statuses.Warning("The operation moved on while asking: nothing was skipped");
            return;
        }

        Begin(
            tabId,
            "skip",
            info,
            token => writer.SkipAsync(location, info.Kind, token),
            result => OperationTexts.SkipReport(info, result));
    }

    // The dialog can outlive the state it was opened in: the tab may have started something else meanwhile.
    private bool CanBegin(TabId tabId, string didNot)
    {
        if (host.Workspace.FindTab(tabId) is null)
        {
            return false;
        }

        if (!gate.IsBusy(tabId))
        {
            return true;
        }

        statuses.Warning($"{BusyNotice}; nothing was {didNot}");
        return false;
    }

    private void Begin(
        TabId tabId,
        string verb,
        OperationInfo info,
        Func<CancellationToken, Task<OperationResult>> run,
        Func<OperationResult, OperationReport> report)
    {
        gate.Begin(tabId);
        var token = cancellationOf(tabId);
        _ = Task.Run(() => RunAsync(tabId, verb, info, run, report, token), CancellationToken.None);
    }

    private async Task RunAsync(
        TabId tabId,
        string verb,
        OperationInfo info,
        Func<CancellationToken, Task<OperationResult>> run,
        Func<OperationResult, OperationReport> report,
        CancellationToken token)
    {
        try
        {
            var done = report(await run(token).ConfigureAwait(false));
            postToUi(() => Finish(tabId, done.Text, done.Severity));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => gate.End(tabId));
        }
        catch (Exception exception)
        {
            var failure = OperationTexts.Failure(verb, info, OperationText.Reason(exception));
            postToUi(() => Finish(tabId, failure, StatusSeverity.Error));
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
