using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Stash;

/// <summary>
/// The stash actions (ux-workflows.md, workflow 7): push with a message prompt popup (untracked files only through
/// their own key), apply, pop and drop on the stash row under the sidebar cursor. Drop asks first and names the undo
/// command. The target is fixed when the action starts and travels as the stash commit, so a stack that changed
/// meanwhile never redirects it. The git call runs off the UI thread; afterwards the tab reads everything again (stash
/// list, refs, status). One operation per tab at a time, shared with staging.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Changes the stash stack.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="refresh">Asks a tab to read everything again; false when it cannot.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog, runs the callback on <c>y</c>; false if it cannot show.</param>
/// <param name="cursorRow">The sidebar row under the cursor of the tab, null when there is none.</param>
/// <param name="fileTarget">The file of the shown stash under the cursor of the detail pane, null when none.</param>
/// <param name="setMode">Switches the keymap mode (Insert while the message is typed).</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with the staging actions.</param>
public sealed class StashController(
    Dispatcher host,
    IStashWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, bool> refresh,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<RepositoryState, SidebarRow?> cursorRow,
    Func<RepositoryState, StashFileTarget?> fileTarget,
    Action<KeyMode> setMode,
    Action redraw,
    TabOperationGate gate) : IPromptHandler
{
    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string NoStashNotice = "Select a stash in the sidebar first";
    private const string NoFileNotice = "Select a file of a shown stash first (Enter on the stash, Ctrl-j to its files)";
    private const string TooSmallNoRestoreNotice = "The screen is too small to ask: nothing was restored";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was dropped";

    private TabId? _pushing;
    private bool _includeUntracked;
    private string _text = string.Empty;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup
    {
        get
        {
            if (_pushing is null)
            {
                return null;
            }

            return PromptPopup.ForText(
                "Stash changes",
                $"{(_includeUntracked ? "Tracked and untracked files" : "Tracked changes")}, message optional",
                _text,
                "stash");
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_pushing is not null)
        {
            return TryExecutePrompt(action);
        }

        if (!Owns(action))
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

    public bool HandleTextKey(KeyEvent key)
    {
        if (_pushing is null)
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

    private static bool Owns(ActionId action) =>
        action == ActionIds.StashPush || action == ActionIds.StashPushUntracked || action == ActionIds.StashApply
        || action == ActionIds.StashPop || action == ActionIds.StashDrop || action == ActionIds.StashRestoreFile;

    private bool TryExecutePrompt(ActionId action)
    {
        if (action == ActionIds.InputAccept && _pushing is { } id)
        {
            var message = _text.Trim();
            var includeUntracked = _includeUntracked;
            ClosePrompt();
            if (host.Workspace.FindTab(id) is { } tab)
            {
                Notice = gate.IsBusy(id) ? BusyNotice : string.Empty;
                if (Notice.Length == 0)
                {
                    StartPush(tab, message, includeUntracked);
                }
            }

            return true;
        }

        if (action == ActionIds.Cancel)
        {
            ClosePrompt();
            return true;
        }

        return false;
    }

    private string Start(RepositoryState tab, ActionId action)
    {
        if (action == ActionIds.StashPush || action == ActionIds.StashPushUntracked)
        {
            _pushing = tab.Id;
            _includeUntracked = action == ActionIds.StashPushUntracked;
            _text = string.Empty;
            setMode(KeyMode.Insert);
            redraw();
            return string.Empty;
        }

        if (action == ActionIds.StashRestoreFile)
        {
            if (fileTarget(tab) is not { } target)
            {
                return NoFileNotice;
            }

            StartRestoreCheck(tab, target);
            return string.Empty;
        }

        var entry = TargetOf(tab);
        if (entry is null)
        {
            return NoStashNotice;
        }

        if (action == ActionIds.StashDrop)
        {
            return confirm(DropPrompt(entry), () => StartDrop(tab, entry)) ? string.Empty : TooSmallNotice;
        }

        StartApply(tab, entry, action == ActionIds.StashPop);
        return string.Empty;
    }

    private StashEntry? TargetOf(RepositoryState tab)
    {
        if (tab.View.FocusedPane != PaneKind.Sidebar || cursorRow(tab) is not { Kind: SidebarRowKind.Stash } row)
        {
            return null;
        }

        return tab.Stashes.FirstOrDefault(entry => entry.CommitHash == row.CommitHash);
    }

    private void ClosePrompt()
    {
        _pushing = null;
        setMode(KeyMode.Normal);
        redraw();
    }

    private static ConfirmPrompt DropPrompt(StashEntry entry) => new(
        "Drop stash",
        [$"Drop {entry.Name}: {entry.Message}?"],
        $"Undo: {RestoreCommand(entry)}",
        "Drop stash");

    private static string RestoreCommand(StashEntry entry) =>
        $"git stash store -m \"{entry.Message.Replace("\"", "\\\"", StringComparison.Ordinal)}\" {entry.CommitHash}";

    private void StartPush(RepositoryState tab, string message, bool includeUntracked)
    {
        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(
            () => RunAsync(
                tab.Id,
                "stash",
                () => writer.PushAsync(tab.Location, message, includeUntracked, token),
                result => PushReport(result, message, includeUntracked),
                token),
            CancellationToken.None);
    }

    private void StartApply(RepositoryState tab, StashEntry entry, bool pop)
    {
        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        var verb = pop ? "pop" : "apply";
        _ = Task.Run(
            () => RunAsync(
                tab.Id,
                verb,
                () => pop ? writer.PopAsync(tab.Location, entry, token) : writer.ApplyAsync(tab.Location, entry, token),
                result => ApplyReport(result, entry, pop),
                token),
            CancellationToken.None);
    }

    private void StartDrop(RepositoryState tab, StashEntry entry)
    {
        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was dropped");
            return;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(
            () => RunAsync(
                tab.Id,
                "drop",
                () => writer.DropAsync(tab.Location, entry, token),
                result => DropReport(result, entry),
                token),
            CancellationToken.None);
    }

    private void StartRestoreCheck(RepositoryState tab, StashFileTarget target)
    {
        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => CheckRestoreAsync(tab, target, token), CancellationToken.None);
    }

    private async Task CheckRestoreAsync(RepositoryState tab, StashFileTarget target, CancellationToken token)
    {
        try
        {
            var state = await writer.CheckRestoreAsync(tab.Location, target.Entry, target.File, token)
                .ConfigureAwait(false);
            postToUi(() => AfterRestoreCheck(tab, target, state));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not restore {target.File.Change.Path}: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error));
        }
    }

    private void AfterRestoreCheck(RepositoryState tab, StashFileTarget target, StashRestoreState state)
    {
        gate.End(tab.Id);
        if (state == StashRestoreState.Overwrites)
        {
            if (!confirm(RestorePrompt(target), () => StartRestore(tab, target)))
            {
                statuses.Warning(TooSmallNoRestoreNotice);
            }

            return;
        }

        if (state == StashRestoreState.Creates)
        {
            StartRestore(tab, target);
            return;
        }

        var report = RestoreReport(new StashRestoreResult(state, null), target);
        statuses.Post(report.Text, report.Severity);
    }

    private void StartRestore(RepositoryState tab, StashFileTarget target)
    {
        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was restored");
            return;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunRestoreAsync(tab, target, token), CancellationToken.None);
    }

    private async Task RunRestoreAsync(RepositoryState tab, StashFileTarget target, CancellationToken token)
    {
        try
        {
            var result = await writer.RestoreFileAsync(tab.Location, target.Entry, target.File, token)
                .ConfigureAwait(false);
            var done = RestoreReport(result, target);
            postToUi(() => Finish(tab.Id, done.Text, done.Severity));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not restore {target.File.Change.Path}: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error));
        }
    }

    private static ConfirmPrompt RestorePrompt(StashFileTarget target) => new(
        "Restore file from stash",
        [
            $"Replace {target.File.Change.Path} in the working tree with its version from {target.Entry.Name}?",
            "Your current content of the file is replaced; the stash and the index stay as they are.",
        ],
        "Undo: your current content is saved as a git object first; its id is shown afterwards",
        "Restore file");

    private static StashReport RestoreReport(StashRestoreResult result, StashFileTarget target)
    {
        var path = target.File.Change.Path;
        var name = target.Entry.Name;
        return result.State switch
        {
            StashRestoreState.Missing => Gone(target.Entry),
            StashRestoreState.NotInStash => new StashReport(
                $"{name} holds no copy of {path} to restore (the stash deletes it): nothing was changed",
                StatusSeverity.Warning),
            StashRestoreState.Identical => new StashReport(
                $"{path} already matches {name}: nothing to restore",
                StatusSeverity.Info),
            _ => new StashReport(RestoredText(result, path, name), StatusSeverity.Info),
        };
    }

    private static string RestoredText(StashRestoreResult result, string path, string name)
    {
        if (result.BackupBlob is null)
        {
            return $"Restored {path} from {name}";
        }

        return $"Restored {path} from {name}; undo: git cat-file blob {result.BackupBlob} > \"{path}\"";
    }

    private async Task RunAsync(
        TabId tabId,
        string verb,
        Func<Task<StashResult>> run,
        Func<StashResult, StashReport> report,
        CancellationToken token)
    {
        try
        {
            var result = await run().ConfigureAwait(false);
            var done = report(result);
            postToUi(() => Finish(tabId, done.Text, done.Severity));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => gate.End(tabId));
        }
        catch (Exception exception)
        {
            var failure = $"Could not {verb} the stash: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tabId, failure, StatusSeverity.Error));
        }
    }

    private static StashReport PushReport(StashResult result, string message, bool includeUntracked)
    {
        if (result.Outcome == StashOutcome.NothingToStash)
        {
            var hint = includeUntracked ? string.Empty : " (untracked files are stashed with <space>ku)";
            return new StashReport($"Nothing to stash{hint}", StatusSeverity.Warning);
        }

        var named = message.Length > 0 ? $" \"{message}\"" : string.Empty;
        return new StashReport($"Stashed{named} as {ShortHash.Of(result.CommitHash)}", StatusSeverity.Info);
    }

    private static StashReport ApplyReport(StashResult result, StashEntry entry, bool pop)
    {
        if (result.Outcome == StashOutcome.Missing)
        {
            return Gone(entry);
        }

        if (result.Outcome == StashOutcome.Conflicted)
        {
            return new StashReport(
                $"Applied {entry.Name} with conflicts: the files are marked !, the stash is kept",
                StatusSeverity.Warning);
        }

        if (pop)
        {
            return new StashReport($"Popped {entry.Name}; undo the drop: {RestoreCommand(entry)}", StatusSeverity.Info);
        }

        return new StashReport($"Applied {entry.Name}; the stash is kept", StatusSeverity.Info);
    }

    private static StashReport DropReport(StashResult result, StashEntry entry)
    {
        if (result.Outcome == StashOutcome.Missing)
        {
            return Gone(entry);
        }

        return new StashReport(
            $"Dropped {entry.Name} ({entry.CommitHash}); undo: {RestoreCommand(entry)}",
            StatusSeverity.Info);
    }

    private static StashReport Gone(StashEntry entry) =>
        new($"{entry.Name} is no longer in the stash list: nothing was changed", StatusSeverity.Warning);

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
