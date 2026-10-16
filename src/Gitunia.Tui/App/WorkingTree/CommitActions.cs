using Gitunia.Core.Editing;
using Gitunia.Core.Overlays;
using Gitunia.Git.Status.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// The commit part of the working-tree view (ux.md, workflow 2, "Message and commit"): the draft of each tab (message, options,
/// amend), the message box's Normal and Insert behaviour, and the commit itself. <c>git commit</c> runs off the UI thread and is
/// cancellable (<c>Esc</c> while it runs); the box is read-only meanwhile. A refused commit says why without running git, a
/// failed one keeps the draft and shows git's and the hooks' output in the output overlay, a successful one clears the draft,
/// re-reads the status and returns focus to the lists.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Creates the commit.</param>
/// <param name="operations">Tells whether a merge, cherry-pick or revert is in progress.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="refreshStatus">Asks a tab to re-read its status and calls back when the new state is in; false when it cannot.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="showOutput">Shows a failed command's output (title, heading, lines).</param>
/// <param name="keyMode">The current input mode.</param>
/// <param name="setMode">Switches the input mode.</param>
/// <param name="route">Runs an action as if its key had been pressed (for keys this class passes on).</param>
/// <param name="redraw">Redraws the screen.</param>
/// <param name="showConfirm">Shows a Confirm dialog and runs the callback on <c>y</c>; false when it does not fit. Null never asks.</param>
public sealed class CommitActions(
    WorkspaceHost host,
    ICommitWriter writer,
    IRepositoryOperationDetector operations,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, Action, bool> refreshStatus,
    Func<TabId, CancellationToken> cancellationOf,
    Action<string, string, IReadOnlyList<string>> showOutput,
    Func<KeyMode> keyMode,
    Action<KeyMode> setMode,
    Action<ActionId, int?> route,
    Action redraw,
    Func<ConfirmPrompt, Action, bool>? showConfirm = null)
{
    private const string StageAllQuestion = "Nothing is staged. Stage all changes and commit?";
    private const int ShortHashLength = 7;
    private const string CancelledNotice = "Commit cancelled: check the history, git may have finished first";

    private readonly Dictionary<TabId, CommitDraft> _drafts = [];
    private readonly Dictionary<TabId, CancellationTokenSource> _running = [];
    private bool _ownsInsert;

    /// <summary>
    /// Stages every unstaged file of a tab and calls back once the status shows it; returns a notice when nothing started.
    /// Set by the application once the staging actions exist.
    /// </summary>
    public Func<RepositoryState, Action, string>? StageAllThen { get; set; }

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>True for the actions this class runs.</summary>
    public static bool Handles(ActionId action) =>
        action == ActionIds.WorkingTreeWriteMessage
        || action == ActionIds.WorkingTreeEditMessage
        || action == ActionIds.WorkingTreeMessageNewLine
        || action == ActionIds.WorkingTreeCommit
        || action == ActionIds.WorkingTreeToggleSignOff
        || action == ActionIds.WorkingTreeToggleNoVerify
        || action == ActionIds.WorkingTreeToggleAmend;

    /// <summary>The draft of the tab; null while the tab never had one.</summary>
    public CommitDraft? DraftOf(TabId tabId) => _drafts.GetValueOrDefault(tabId);

    /// <summary>The draft of the tab, created empty when the tab has none yet.</summary>
    public CommitDraft DraftFor(TabId tabId)
    {
        if (!_drafts.TryGetValue(tabId, out var draft))
        {
            draft = new CommitDraft();
            _drafts[tabId] = draft;
        }

        return draft;
    }

    /// <summary>True while the message box of the active tab takes typed keys.</summary>
    public bool IsEditing =>
        _ownsInsert
        && keyMode() == KeyMode.Insert
        && host.Workspace.ActiveTab?.View.FocusedPane == PaneKind.WorkingMessage;

    /// <summary>The names of the tabs that hold a message that was not committed.</summary>
    public IReadOnlyList<string> TabsWithUnsentMessage() =>
        [
            .. host.Workspace.Tabs
                .Where(tab => _drafts.TryGetValue(tab.Id, out var draft) && draft.HasUnsentText)
                .Select(tab => tab.Title),
        ];

    /// <summary>Forgets the drafts of tabs that are gone; call when the workspace changed.</summary>
    public void ForgetClosedTabs()
    {
        foreach (var tabId in _drafts.Keys.Where(id => host.Workspace.FindTab(id) is null).ToList())
        {
            _drafts.Remove(tabId);

            // The running commit's callback disposes the source.
            if (_running.TryGetValue(tabId, out var source))
            {
                source.Cancel();
            }
        }

        LeaveInsertWhenFocusMoved();
    }

    /// <summary>Ends Insert mode when the box that owned it lost focus (a click, <c>Tab</c> or a tab switch).</summary>
    public void LeaveInsertWhenFocusMoved()
    {
        if (!_ownsInsert)
        {
            return;
        }

        var active = host.Workspace.ActiveTab;
        var isInBox = active is { View: { Mode: TabViewMode.WorkingTree, FocusedPane: PaneKind.WorkingMessage } };
        if (!isInBox)
        {
            _ownsInsert = false;
            if (keyMode() == KeyMode.Insert)
            {
                setMode(KeyMode.Normal);
            }
        }
        else if (keyMode() != KeyMode.Insert)
        {
            _ownsInsert = false;
        }
    }

    /// <summary>Runs the action when it concerns the message box; false lets the other handlers have it.</summary>
    /// <param name="tab">The active tab, showing the working-tree view.</param>
    /// <param name="action">The action.</param>
    public bool TryExecute(RepositoryState tab, ActionId action)
    {
        Notice = string.Empty;
        var isInBox = tab.View.FocusedPane == PaneKind.WorkingMessage;
        if ((action == ActionIds.Cancel || action == ActionIds.NavigateBack) && TryCancelRunning(tab))
        {
            return true;
        }

        if (action == ActionIds.Cancel)
        {
            return TryStopEditing(isInBox);
        }

        if (action == ActionIds.NavigateBack && isInBox)
        {
            Send(tab, new FocusPaneCommand(ListPaneOf(tab)));
            return true;
        }

        if (!Handles(action))
        {
            return false;
        }

        Notice = Run(tab, action);
        redraw();
        return true;
    }

    /// <summary>Gives a key typed in Insert mode to the editor when the box owns Insert mode; true when it took the key.</summary>
    /// <param name="key">The key.</param>
    public bool HandleTextKey(KeyEvent key)
    {
        if (!IsEditing
            || host.Workspace.ActiveTab is not { } tab
            || DraftOf(tab.Id) is not { IsCommitting: false } draft)
        {
            return false;
        }

        draft.Editor.Apply(key);
        redraw();
        return true;
    }

    private string Run(RepositoryState tab, ActionId action)
    {
        var draft = DraftFor(tab.Id);
        if (draft.IsCommitting && action != ActionIds.WorkingTreeMessageNewLine)
        {
            return "A commit is running: Esc cancels it";
        }

        if (action == ActionIds.WorkingTreeWriteMessage || action == ActionIds.WorkingTreeEditMessage)
        {
            return StartEditing(tab, draft);
        }

        if (action == ActionIds.WorkingTreeMessageNewLine)
        {
            return NewLine(draft);
        }

        if (action == ActionIds.WorkingTreeCommit)
        {
            return Commit(tab, draft);
        }

        if (action == ActionIds.WorkingTreeToggleSignOff)
        {
            draft.SignOff = !draft.SignOff;
            return $"SignOff {OnOff(draft.SignOff)}";
        }

        if (action == ActionIds.WorkingTreeToggleNoVerify)
        {
            draft.NoVerify = !draft.NoVerify;
            return $"No-Verify {OnOff(draft.NoVerify)}: hooks {(draft.NoVerify ? "are skipped" : "run")}";
        }

        return ToggleAmend(tab, draft);
    }

    private string StartEditing(RepositoryState tab, CommitDraft draft)
    {
        if (tab.View.FocusedPane != PaneKind.WorkingMessage)
        {
            Send(tab, new FocusPaneCommand(PaneKind.WorkingMessage));
        }

        StartMergeMessageRead(tab, draft);
        _ownsInsert = true;
        setMode(KeyMode.Insert);
        return string.Empty;
    }

    // Finishing a merge: the box starts with git's prepared message, which the user may edit.
    private void StartMergeMessageRead(RepositoryState tab, CommitDraft draft)
    {
        if (!draft.Editor.IsBlank || operations.Detect(tab.Location) != RepositoryOperation.Merge)
        {
            return;
        }

        var location = tab.Location;
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => ReadMergeMessageAsync(location, draft, token), CancellationToken.None);
    }

    private async Task ReadMergeMessageAsync(RepositoryLocation location, CommitDraft draft, CancellationToken token)
    {
        try
        {
            var message = await writer.ReadMergeMessageAsync(location, token).ConfigureAwait(false);
            if (message is not null)
            {
                postToUi(() => PrefillMessage(draft, message));
            }
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
    }

    private void PrefillMessage(CommitDraft draft, string message)
    {
        draft.Prefill(message);
        redraw();
    }

    private string NewLine(CommitDraft draft)
    {
        if (!IsEditing)
        {
            // Enter in another prompt's Insert mode (the tab rename) is that prompt's accept.
            route(ActionIds.InputAccept, null);
        }
        else if (!draft.IsCommitting)
        {
            draft.Editor.Apply(new KeyEvent(KeyNames.Enter));
        }

        return string.Empty;
    }

    private string ToggleAmend(RepositoryState tab, CommitDraft draft)
    {
        if (draft.Amend)
        {
            draft.SetAmend(false);
            return "Amend off";
        }

        if (CommitCheck.AmendRefusal(tab.WorkingTree, operations.Detect(tab.Location)) is { } refusal)
        {
            return refusal;
        }

        draft.SetAmend(true);
        var location = tab.Location;
        var hash = tab.Head?.CommitHash;
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => ReadHeadMessageAsync(location, hash, draft, token), CancellationToken.None);
        return "Amend on: the staged changes join the last commit";
    }

    private async Task ReadHeadMessageAsync(
        RepositoryLocation location,
        string? hash,
        CommitDraft draft,
        CancellationToken token)
    {
        try
        {
            var message = await writer.ReadHeadMessageAsync(location, token).ConfigureAwait(false);
            if (message is not null)
            {
                postToUi(() => ApplyHeadMessage(draft, hash, message));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var notice = $"Could not read the last commit's message: {CommitFailureReport.Notice(exception.Message)}";
            postToUi(() => statuses.Error(notice));
        }
    }

    private void ApplyHeadMessage(CommitDraft draft, string? hash, string message)
    {
        draft.ApplyHeadMessage(Short(hash ?? string.Empty), message);
        redraw();
    }

    private string Commit(RepositoryState tab, CommitDraft draft)
    {
        var operation = operations.Detect(tab.Location);
        var refusal = CommitCheck.Refusal(draft, tab.WorkingTree, operation);
        if (refusal == CommitCheck.NothingStaged && ShouldOfferStageAll(tab))
        {
            return OfferStageAll(tab, draft);
        }

        if (refusal is not null)
        {
            if (refusal == CommitCheck.EmptyMessage && tab.View.FocusedPane != PaneKind.WorkingMessage)
            {
                // The box keeps focus on an empty message, so the user can type it.
                StartEditing(tab, draft);
            }

            return refusal;
        }

        return StartCommit(tab, draft);
    }

    // Nothing staged but unstaged changes exist: ask before staging them (the draft stays as it is on no).
    private bool ShouldOfferStageAll(RepositoryState tab) =>
        showConfirm is not null
        && StageAllThen is not null
        && tab.WorkingTree is { Staged.Count: 0 } tree
        && tree.Unstaged.Any(entry => entry.Kind != StatusEntryKind.Unmerged);

    private string OfferStageAll(RepositoryState tab, CommitDraft draft)
    {
        var tabId = tab.Id;
        var prompt = new ConfirmPrompt(
            "Commit",
            [StageAllQuestion],
            "Untracked files are included; conflicted files are skipped. Unstage them again with <space>U.",
            "Stage all and commit");
        if (!showConfirm!(prompt, () => StageAllAndCommit(tabId, draft)))
        {
            return CommitCheck.NothingStaged;
        }

        return string.Empty;
    }

    private void StageAllAndCommit(TabId tabId, CommitDraft draft)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || draft.IsCommitting)
        {
            return;
        }

        var notice = StageAllThen!(tab, () => CommitAfterStaging(tabId, draft));
        if (notice.Length > 0)
        {
            statuses.Warning(notice);
        }
    }

    private void CommitAfterStaging(TabId tabId, CommitDraft draft)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || draft.IsCommitting)
        {
            return;
        }

        var operation = operations.Detect(tab.Location);
        var refusal = CommitCheck.Refusal(draft, tab.WorkingTree, operation);
        if (refusal is not null)
        {
            statuses.Warning(refusal);
        }
        else
        {
            var notice = StartCommit(tab, draft);
            if (notice.Length > 0)
            {
                statuses.Warning(notice);
            }
        }

        redraw();
    }

    private string StartCommit(RepositoryState tab, CommitDraft draft)
    {
        var request = new CommitRequest(draft.Editor.Text, draft.SignOff, draft.NoVerify, draft.Amend);
        var needsForcePush = draft.Amend && CommitCheck.NeedsForcePush(tab.WorkingTree);
        var upstream = needsForcePush ? tab.WorkingTree?.Snapshot.Branch.UpstreamName : null;
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationOf(tab.Id));
        _running[tab.Id] = source;
        draft.IsCommitting = true;
        var location = tab.Location;
        var tabId = tab.Id;
        var token = source.Token;
        _ = Task.Run(() => RunCommitAsync(tabId, location, draft, request, upstream, token), CancellationToken.None);
        return string.Empty;
    }

    private async Task RunCommitAsync(
        TabId tabId,
        RepositoryLocation location,
        CommitDraft draft,
        CommitRequest request,
        string? forcePushUpstream,
        CancellationToken token)
    {
        try
        {
            var result = await writer.CommitAsync(location, request, token).ConfigureAwait(false);
            postToUi(() => Succeeded(tabId, draft, request, result, forcePushUpstream));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Failed(tabId, draft, CancelledNotice, null));
        }
        catch (GitCommandFailedException exception)
        {
            var notice = $"Commit failed: {CommitFailureReport.Notice(exception.StandardError)}";
            postToUi(() => Failed(tabId, draft, notice, exception.StandardError));
        }
        catch (Exception exception)
        {
            var notice = $"Commit failed: {CommitFailureReport.Notice(exception.Message)}";
            postToUi(() => Failed(tabId, draft, notice, null));
        }
    }

    private void Succeeded(
        TabId tabId,
        CommitDraft draft,
        CommitRequest request,
        CommitResult result,
        string? forcePushUpstream)
    {
        ReleaseSource(tabId);
        draft.IsCommitting = false;
        draft.Sent();
        statuses.Post(SuccessMessage(request, result, forcePushUpstream));
        if (host.Workspace.FindTab(tabId) is { } tab)
        {
            LeaveBox(tab);
            if (!refreshStatus(tabId, redraw))
            {
                redraw();
            }
        }
    }

    private void Failed(TabId tabId, CommitDraft draft, string message, string? output)
    {
        ReleaseSource(tabId);
        draft.IsCommitting = false;
        statuses.Error(message);
        if (output is not null && host.Workspace.ActiveTabId == tabId)
        {
            showOutput(CommitFailureReport.Title, CommitFailureReport.Heading, CommitFailureReport.Lines(output));
        }

        // A hook may have changed files, and a cancelled commit may have got as far as the index.
        if (host.Workspace.FindTab(tabId) is not null && !refreshStatus(tabId, redraw))
        {
            redraw();
        }
    }

    private static string SuccessMessage(CommitRequest request, CommitResult result, string? forcePushUpstream)
    {
        var hash = Short(result.Hash);
        if (!request.Amend || result.PreviousHash is not { } previous)
        {
            return $"Committed {hash}";
        }

        var old = Short(previous);
        var warning = forcePushUpstream is null
            ? string.Empty
            : $"; it was on {forcePushUpstream}: pushing needs a force push";
        return $"Amended to {hash}; previous commit {old}, `git reset --soft {old}` restores it{warning}";
    }

    // Esc (Cancel, or NavigateBack in Normal mode) stops a running commit wherever the focus is.
    private bool TryCancelRunning(RepositoryState tab)
    {
        if (!_running.TryGetValue(tab.Id, out var source))
        {
            return false;
        }

        source.Cancel();
        Notice = "Cancelling the commit...";
        redraw();
        return true;
    }

    private bool TryStopEditing(bool isInBox)
    {
        if (!isInBox || !IsEditing)
        {
            return false;
        }

        _ownsInsert = false;
        setMode(KeyMode.Normal);
        redraw();
        return true;
    }

    // The source is disposed here, on the UI thread, once nothing can cancel it any more.
    private void ReleaseSource(TabId tabId)
    {
        if (_running.Remove(tabId, out var source))
        {
            source.Dispose();
        }
    }

    // After a commit, focus goes back to a list and Insert mode ends.
    private void LeaveBox(RepositoryState tab)
    {
        if (tab.View.Mode == TabViewMode.WorkingTree && tab.View.FocusedPane == PaneKind.WorkingMessage)
        {
            Send(tab, new FocusPaneCommand(ListPaneOf(tab)));
        }

        LeaveInsertWhenFocusMoved();
    }

    private static PaneKind ListPaneOf(RepositoryState tab) =>
        tab.WorkingTree?.Selection?.Section == WorkingTreeSection.Staged
            ? PaneKind.WorkingStaged
            : PaneKind.WorkingUnstaged;

    private static string OnOff(bool isOn) => isOn ? "on" : "off";

    private static string Short(string hash) => hash.Length > ShortHashLength ? hash[..ShortHashLength] : hash;

    private void Send(RepositoryState tab, RepositoryCommand command) => host.Dispatch(new TabCommand(tab.Id, command));
}
