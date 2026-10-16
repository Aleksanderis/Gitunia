using Gitunia.Core.Editing;
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
    Action redraw)
{
    private const int ShortHashLength = 7;
    private const string MergeMessageFile = "MERGE_MSG";
    private const char CommentMark = '#';

    private readonly Dictionary<TabId, CommitDraft> _drafts = [];
    private readonly Dictionary<TabId, CancellationTokenSource> _running = [];
    private bool _ownsInsert;

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
        _ownsInsert && keyMode() == KeyMode.Insert && host.Workspace.ActiveTab?.View.FocusedPane == PaneKind.WorkingMessage;

    /// <summary>The names of the tabs that hold a message that was not committed.</summary>
    public IReadOnlyList<string> TabsWithUnsentMessage() =>
        [.. host.Workspace.Tabs.Where(tab => _drafts.TryGetValue(tab.Id, out var draft) && draft.HasUnsentText).Select(tab => tab.Title)];

    /// <summary>Forgets the drafts of tabs that are gone; call when the workspace changed.</summary>
    public void ForgetClosedTabs()
    {
        foreach (var tabId in _drafts.Keys.Where(id => host.Workspace.FindTab(id) is null).ToList())
        {
            _drafts.Remove(tabId);
            if (_running.Remove(tabId, out var source))
            {
                source.Cancel();
            }
        }

        LeaveInsertWhenFocusMoved();
    }

    /// <summary>Runs the action when it concerns the message box; false lets the other handlers have it.</summary>
    /// <param name="tab">The active tab, showing the working-tree view.</param>
    /// <param name="action">The action.</param>
    public bool TryExecute(RepositoryState tab, ActionId action)
    {
        Notice = string.Empty;
        var isInBox = tab.View.FocusedPane == PaneKind.WorkingMessage;
        if (action == ActionIds.Cancel)
        {
            return TryCancel(tab, isInBox);
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
        if (!IsEditing || host.Workspace.ActiveTab is not { } tab || DraftOf(tab.Id) is not { IsCommitting: false } draft)
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

        PrefillFromMergeMessage(tab, draft);
        _ownsInsert = true;
        setMode(KeyMode.Insert);
        return string.Empty;
    }

    // Finishing a merge: the box starts with git's prepared message (comment lines dropped), which the user may edit.
    private void PrefillFromMergeMessage(RepositoryState tab, CommitDraft draft)
    {
        if (!draft.Editor.IsBlank || operations.Detect(tab.Location) != RepositoryOperation.Merge)
        {
            return;
        }

        try
        {
            var path = Path.Combine(tab.Location.GitDirectory, MergeMessageFile);
            var lines = File.ReadAllLines(path).Where(line => !line.StartsWith(CommentMark));
            draft.Editor.SetText(string.Join('\n', lines).TrimEnd());
        }
        catch (IOException)
        {
            // The merge finished while we looked: an empty box is right.
        }
    }

    private string NewLine(CommitDraft draft)
    {
        if (!IsEditing)
        {
            // Enter in another prompt's Insert mode (the tab rename) is that prompt's accept.
            route(ActionIds.InputAccept, null);
            return string.Empty;
        }

        draft.Editor.Apply(new KeyEvent(KeyNames.Enter));
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

    private async Task ReadHeadMessageAsync(RepositoryLocation location, string? hash, CommitDraft draft, CancellationToken token)
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
            postToUi(() => statuses.Error($"Could not read the last commit's message: {CommitFailureReport.Notice(exception.Message)}"));
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
        if (CommitCheck.Refusal(draft, tab.WorkingTree, operation) is { } refusal)
        {
            if (refusal == CommitCheck.EmptyMessage && tab.View.FocusedPane != PaneKind.WorkingMessage)
            {
                // The box keeps focus on an empty message, so the user can type it.
                StartEditing(tab, draft);
            }

            return refusal;
        }

        var request = new CommitRequest(draft.Editor.Text, draft.SignOff, draft.NoVerify, draft.Amend);
        var needsForcePush = draft.Amend && CommitCheck.NeedsForcePush(tab.WorkingTree);
        var upstream = tab.WorkingTree?.Snapshot.Branch.UpstreamName ?? string.Empty;
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationOf(tab.Id));
        _running[tab.Id] = source;
        draft.IsCommitting = true;
        var location = tab.Location;
        var tabId = tab.Id;
        _ = Task.Run(() => RunCommitAsync(tabId, location, draft, request, needsForcePush ? upstream : null, source), CancellationToken.None);
        return string.Empty;
    }

    private async Task RunCommitAsync(
        TabId tabId,
        RepositoryLocation location,
        CommitDraft draft,
        CommitRequest request,
        string? forcePushUpstream,
        CancellationTokenSource source)
    {
        try
        {
            var result = await writer.CommitAsync(location, request, source.Token).ConfigureAwait(false);
            postToUi(() => Succeeded(tabId, draft, request, result, forcePushUpstream));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Failed(tabId, draft, "Commit cancelled", null));
        }
        catch (GitCommandFailedException exception)
        {
            postToUi(() => Failed(tabId, draft, $"Commit failed: {CommitFailureReport.Notice(exception.StandardError)}", exception.StandardError));
        }
        catch (Exception exception)
        {
            postToUi(() => Failed(tabId, draft, $"Commit failed: {CommitFailureReport.Notice(exception.Message)}", null));
        }
        finally
        {
            source.Dispose();
        }
    }

    private void Succeeded(TabId tabId, CommitDraft draft, CommitRequest request, CommitResult result, string? forcePushUpstream)
    {
        _running.Remove(tabId);
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
        _running.Remove(tabId);
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
        var warning = forcePushUpstream is null ? string.Empty : $"; it was on {forcePushUpstream}: pushing needs a force push";
        return $"Amended to {hash}; previous commit {old}, `git reset --soft {old}` restores it{warning}";
    }

    private bool TryCancel(RepositoryState tab, bool isInBox)
    {
        if (_running.TryGetValue(tab.Id, out var source))
        {
            source.Cancel();
            Notice = "Cancelling the commit...";
            redraw();
            return true;
        }

        if (isInBox && IsEditing)
        {
            _ownsInsert = false;
            setMode(KeyMode.Normal);
            redraw();
            return true;
        }

        return false;
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

    private static PaneKind ListPaneOf(RepositoryState tab) =>
        tab.WorkingTree?.Selection?.Section == WorkingTreeSection.Staged ? PaneKind.WorkingStaged : PaneKind.WorkingUnstaged;

    private static string OnOff(bool isOn) => isOn ? "on" : "off";

    private static string Short(string hash) => hash.Length > ShortHashLength ? hash[..ShortHashLength] : hash;

    private void Send(RepositoryState tab, RepositoryCommand command) => host.Dispatch(new TabCommand(tab.Id, command));
}
