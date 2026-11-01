using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Worktrees;
using Gitunia.Git.Worktrees.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Worktrees;

/// <summary>
/// The worktree operations of the sidebar (ux-workflows.md, workflow 6): add (a branch prompt, then a path prompt), remove (a
/// Confirm, and a second one that lists the changes a dirty worktree would lose), lock (optional reason), unlock
/// and prune (a Confirm listing the entries). Refusals run no git and say why. What is acted on is fixed when the
/// dialog opens; git runs off the UI thread and the tab reads its repository again afterwards. One operation per
/// tab at a time, shared with staging and discarding.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs the git calls.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="refresh">Reads the repository of a tab again; false when it cannot.</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when the dialog cannot
/// be shown.</param>
/// <param name="gate">The one-operation-per-tab gate.</param>
/// <param name="selectedRow">The sidebar row under the cursor of the active tab.</param>
/// <param name="selectedCommit">The commit selected in the graph (the start point of a new branch), null for
/// none.</param>
/// <param name="setMode">Switches the input mode (Insert while a prompt is open).</param>
/// <param name="redraw">Redraws the status bar.</param>
public sealed class WorktreeActions(
    WorkspaceHost host,
    IWorktreeWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<TabId, bool> refresh,
    Func<ConfirmPrompt, Action, bool> confirm,
    TabOperationGate gate,
    Func<SidebarRow?> selectedRow,
    Func<string?> selectedCommit,
    Action<KeyMode> setMode,
    Action redraw) : IPromptHandler
{
    private const string BusyNotice = "Busy: another operation on this tab is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";
    private const int ShortHashLength = 7;
    private const string NoWorktreeNotice = "Select a worktree in the sidebar first";

    private PromptStage _stage = PromptStage.None;
    private TabId _tabId;
    private string _text = string.Empty;
    private string _branch = string.Empty;
    private string? _startPoint;
    private string? _selectedStart;
    private string _target = string.Empty;
    private bool _openAfterAdd;

    private enum PromptStage
    {
        None,
        Branch,
        Path,
        LockReason,
    }

    /// <summary>Opens a new worktree as its own tab; set by the composition root.</summary>
    public Action<TabId, string>? OpenWorktree { get; set; }

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup => _stage switch
    {
        PromptStage.Branch => PromptPopup.ForText(
            "New worktree", "Branch: an existing one, or a new name", _text, "next"),
        PromptStage.Path => PromptPopup.ForText(
            "Worktree path", $"Directory for {_branch}{StartText()}", _text, "add"),
        PromptStage.LockReason => PromptPopup.ForText(
            "Lock worktree", $"Reason for locking {_target}, optional", _text, "lock"),
        _ => null,
    };

    /// <summary>True while a prompt of this controller owns Insert mode.</summary>
    public bool IsPrompting => _stage != PromptStage.None;

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_stage != PromptStage.None)
        {
            return ExecuteInPrompt(action);
        }

        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        if (action == ActionIds.WorktreeAdd)
        {
            BeginAdd(tab);
        }
        else if (action == ActionIds.WorktreeOpen && selectedRow() is { Kind: SidebarRowKind.LocalBranch } branch)
        {
            BeginAddForBranch(tab, branch.Label);
        }
        else if (action == ActionIds.WorktreeRemove)
        {
            Notice = Remove(tab);
        }
        else if (action == ActionIds.WorktreeLock)
        {
            Notice = BeginLock(tab);
        }
        else if (action == ActionIds.WorktreeUnlock)
        {
            Notice = Unlock(tab);
        }
        else if (action == ActionIds.WorktreePrune)
        {
            Notice = Prune(tab);
        }
        else
        {
            return false;
        }

        redraw();
        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (_stage == PromptStage.None)
        {
            return false;
        }

        if (PromptTextEditor.Apply(_text, key) is { } edited)
        {
            _text = edited;
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }

    private bool ExecuteInPrompt(ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            Accept();
        }
        else if (action == ActionIds.Cancel)
        {
            Close();
        }
        else
        {
            return false;
        }

        redraw();
        return true;
    }

    private void Close()
    {
        _stage = PromptStage.None;
        _text = string.Empty;
        setMode(KeyMode.Normal);
    }

    private void Accept()
    {
        var tab = host.Workspace.FindTab(_tabId);
        var stage = _stage;
        var text = _text.Trim();
        Close();
        if (tab is null)
        {
            return;
        }

        if (stage == PromptStage.Branch)
        {
            AcceptBranch(tab, text);
        }
        else if (stage == PromptStage.Path)
        {
            AcceptPath(tab, text);
        }
        else
        {
            RunLock(tab, text);
        }
    }

    private void BeginAdd(RepositoryState tab)
    {
        if (gate.IsBusy(tab.Id))
        {
            Notice = BusyNotice;
            return;
        }

        _openAfterAdd = false;
        _selectedStart = StartPointOf();
        Open(PromptStage.Branch, tab, string.Empty);
    }

    // A local branch without a worktree: ask for the path only, then open the new worktree in its own tab.
    private void BeginAddForBranch(RepositoryState tab, string name)
    {
        if (gate.IsBusy(tab.Id))
        {
            Notice = BusyNotice;
            return;
        }

        _openAfterAdd = true;
        _branch = name;
        _startPoint = null;
        Open(PromptStage.Path, tab, WorktreeTexts.DefaultPath(tab.Location.RootPath, name));
    }

    private void Open(PromptStage stage, RepositoryState tab, string text)
    {
        _stage = stage;
        _tabId = tab.Id;
        _text = text;
        setMode(KeyMode.Insert);
    }

    private void AcceptBranch(RepositoryState tab, string name)
    {
        if (name.Length == 0)
        {
            Notice = "No branch given: nothing was added";
            return;
        }

        var existing = tab.Refs.FirstOrDefault(
            candidate => candidate.Kind == RefKind.LocalBranch && candidate.Name == name);
        if (existing?.CheckedOutIn is { } holder)
        {
            Notice = $"{name} is already checked out in {holder}: a branch can be in one worktree only";
            return;
        }

        _branch = name;
        _startPoint = existing is null ? _selectedStart ?? tab.Head?.CommitHash ?? GitOptions.HeadRevision : null;
        Open(PromptStage.Path, tab, WorktreeTexts.DefaultPath(tab.Location.RootPath, name));
        redraw();
    }

    // A selected worktree or branch row wins over the graph selection; without either git starts at HEAD.
    private string? StartPointOf() =>
        selectedRow() is { Kind: not SidebarRowKind.Stash, CommitHash: { } hash } ? hash : selectedCommit();

    private string StartText() => _startPoint is { } start ? $" (new, at {Shorten(start)})" : " (existing)";

    private static string Shorten(string hash) => hash.Length > ShortHashLength ? hash[..ShortHashLength] : hash;

    private void AcceptPath(RepositoryState tab, string text)
    {
        if (text.Length == 0)
        {
            Notice = "No path given: nothing was added";
            return;
        }

        var root = tab.Location.RootPath;
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root)) ?? root;
        var path = Path.GetFullPath(text, parent);
        var request = new WorktreeAddRequest(path, _branch, _startPoint);
        var done = $"Added worktree {path} on {_branch}";
        Action? afterAdd = _openAfterAdd ? () => OpenWorktree?.Invoke(tab.Id, path) : null;
        Start(tab, token => writer.AddAsync(tab.Location, request, token), done, "add the worktree", afterAdd);
    }

    private string Remove(RepositoryState tab)
    {
        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        if (TargetOf(tab) is not { } worktree)
        {
            return NoWorktreeNotice;
        }

        var refusal = WorktreeTexts.RemoveRefusal(worktree, tab.Location.RootPath, SamePath);
        if (refusal.Length > 0)
        {
            return refusal;
        }

        var shown = confirm(WorktreeTexts.RemovePrompt(worktree), () => StartRemove(tab, worktree));
        return shown ? string.Empty : TooSmallNotice;
    }

    private void StartRemove(RepositoryState tab, WorktreeInfo worktree)
    {
        if (!BeginOperation(tab))
        {
            return;
        }

        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RemoveAsync(tab, worktree, token), CancellationToken.None);
    }

    private async Task RemoveAsync(RepositoryState tab, WorktreeInfo worktree, CancellationToken token)
    {
        try
        {
            var changes = await writer.ListChangesAsync(worktree.Path, token).ConfigureAwait(false);
            if (changes.Count > 0)
            {
                postToUi(() => AskAboutChanges(tab, worktree, changes));
                return;
            }

            await writer.RemoveAsync(tab.Location, worktree.Path, force: false, token).ConfigureAwait(false);
            postToUi(() => Finish(tab.Id, $"Removed worktree {worktree.Path}", StatusSeverity.Info));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not remove the worktree: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error));
        }
    }

    // The first answer was y, but the worktree turned out dirty: nothing is removed unless a second dialog that lists
    // the loss gets y.
    private void AskAboutChanges(RepositoryState tab, WorktreeInfo worktree, IReadOnlyList<string> changes)
    {
        gate.End(tab.Id);
        var shown = confirm(
            WorktreeTexts.DirtyRemovePrompt(worktree, changes),
            () => StartForcedRemove(tab, worktree));
        if (!shown)
        {
            statuses.Warning($"{worktree.Path} has changes and the screen is too small to ask: nothing was removed");
        }
    }

    private void StartForcedRemove(RepositoryState tab, WorktreeInfo worktree)
    {
        var forced = $"Removed worktree {worktree.Path} and its uncommitted changes";
        Start(
            tab,
            token => writer.RemoveAsync(tab.Location, worktree.Path, force: true, token),
            forced,
            "remove the worktree");
    }

    private string BeginLock(RepositoryState tab)
    {
        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        if (TargetOf(tab) is not { } worktree)
        {
            return NoWorktreeNotice;
        }

        if (worktree.IsMain)
        {
            return "The main worktree cannot be locked";
        }

        if (worktree.IsLocked)
        {
            return $"{worktree.Path} is already locked";
        }

        _target = worktree.Path;
        Open(PromptStage.LockReason, tab, string.Empty);
        return string.Empty;
    }

    private void RunLock(RepositoryState tab, string reason)
    {
        var path = _target;
        var done = reason.Length > 0 ? $"Locked worktree {path} ({reason})" : $"Locked worktree {path}";
        Start(tab, token => writer.LockAsync(tab.Location, path, reason, token), done, "lock the worktree");
    }

    private string Unlock(RepositoryState tab)
    {
        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        if (TargetOf(tab) is not { } worktree)
        {
            return NoWorktreeNotice;
        }

        if (!worktree.IsLocked)
        {
            return $"{worktree.Path} is not locked";
        }

        Start(
            tab,
            token => writer.UnlockAsync(tab.Location, worktree.Path, token),
            $"Unlocked worktree {worktree.Path}",
            "unlock the worktree");
        return string.Empty;
    }

    private string Prune(RepositoryState tab)
    {
        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        var prunable = tab.Worktrees.Where(worktree => worktree.IsPrunable && !worktree.IsLocked).ToList();
        if (prunable.Count == 0)
        {
            return "No worktree entry to prune";
        }

        return confirm(WorktreeTexts.PrunePrompt(prunable), () => StartPrune(tab)) ? string.Empty : TooSmallNotice;
    }

    private void StartPrune(RepositoryState tab)
    {
        if (!BeginOperation(tab))
        {
            return;
        }

        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => PruneAsync(tab, token), CancellationToken.None);
    }

    private async Task PruneAsync(RepositoryState tab, CancellationToken token)
    {
        try
        {
            var removed = await writer.PruneAsync(tab.Location, token).ConfigureAwait(false);
            var message = removed.Count == 1 ? "Pruned 1 worktree entry" : $"Pruned {removed.Count} worktree entries";
            postToUi(() => Finish(tab.Id, message, StatusSeverity.Info));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception)
        {
            postToUi(() => Finish(tab.Id, $"Could not prune: {OperationText.Reason(exception)}", StatusSeverity.Error));
        }
    }

    private void Start(
        RepositoryState tab,
        Func<CancellationToken, Task> work,
        string success,
        string verb,
        Action? afterSuccess = null)
    {
        if (!BeginOperation(tab))
        {
            return;
        }

        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab.Id, work, success, verb, afterSuccess, token), CancellationToken.None);
    }

    private async Task RunAsync(
        TabId tabId,
        Func<CancellationToken, Task> work,
        string success,
        string verb,
        Action? afterSuccess,
        CancellationToken token)
    {
        try
        {
            await work(token).ConfigureAwait(false);
            postToUi(() => Finish(tabId, success, StatusSeverity.Info));
            if (afterSuccess is not null)
            {
                postToUi(afterSuccess);
            }
        }
        catch (OperationCanceledException)
        {
            postToUi(() => gate.End(tabId));
        }
        catch (Exception exception)
        {
            postToUi(() => Finish(tabId, $"Could not {verb}: {OperationText.Reason(exception)}", StatusSeverity.Error));
        }
    }

    private bool BeginOperation(RepositoryState tab)
    {
        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was changed");
            return false;
        }

        gate.Begin(tab.Id);
        return true;
    }

    private void Finish(TabId tabId, string message, StatusSeverity severity)
    {
        gate.End(tabId);
        statuses.Post(message, severity);
        refresh(tabId);
    }

    private WorktreeInfo? TargetOf(RepositoryState tab)
    {
        if (selectedRow() is { Kind: SidebarRowKind.Worktree, Path: { } path })
        {
            return tab.Worktrees.FirstOrDefault(worktree => SamePath(worktree.Path, path));
        }

        return null;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.Ordinal);
}
