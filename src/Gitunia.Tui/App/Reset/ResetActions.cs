using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Reset;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Reset;

/// <summary>
/// Moves the current branch to a commit with a soft, mixed or hard reset (ux.md, workflow 3, "Reset"). A one-key
/// status bar prompt picks the mode; soft runs at once, mixed runs at once when nothing is staged, and everything
/// that loses work opens the Confirm dialog, which names the target, the files and the old tip for the undo. Refused
/// on a detached or unborn HEAD and while an operation (merge, rebase, ...) is in progress. The reflog view starts
/// the same flow with <see cref="Begin"/>.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs the reset.</param>
/// <param name="statusReader">Reads what the tab's working tree holds, to name it in the dialog.</param>
/// <param name="operations">Tells whether a merge, rebase or similar is stopped.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="refresh">Reads a tab again (refs, graph, status); false when it cannot.</param>
/// <param name="setMode">Switches the keymap mode (Insert while the mode prompt is open).</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
public sealed class ResetActions(
    WorkspaceHost host,
    IResetWriter writer,
    IStatusReader statusReader,
    IRepositoryOperationDetector operations,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<TabId, bool> refresh,
    Action<KeyMode> setMode,
    Action redraw,
    Func<BranchSelection> selection) : IPromptHandler
{
    private const int ShortHashLength = 7;
    private const int ListedFiles = 6;
    private const string BusyNotice = "Busy: the last reset is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was reset";
    private const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) to reset to";

    private readonly HashSet<TabId> _busy = [];
    private TabId _tabId;
    private string _targetHash = string.Empty;
    private string _branchName = string.Empty;
    private bool _isOpen;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => !_isOpen || host.Workspace.ActiveTabId != _tabId
        ? string.Empty
        : $"reset {_branchName} to {ShortHash(_targetHash)}: [s]oft  [m]ixed  [h]ard  (Esc cancels)";

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_isOpen && host.Workspace.ActiveTabId != _tabId)
        {
            // The prompt belongs to the tab it was opened in; leaving that tab drops it.
            Close();
        }

        if (_isOpen)
        {
            return TryHandleOpenPrompt(action);
        }

        if (action != ActionIds.ResetBranch)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is { } tab)
        {
            Notice = selection().CommitHash is { } hash ? Begin(tab.Id, hash) : SelectNotice;
        }

        redraw();
        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (!_isOpen || host.Workspace.ActiveTabId != _tabId)
        {
            return false;
        }

        var mode = key.Normalize() is { Modifiers: KeyModifiers.None } normalized ? ModeOf(normalized.Name) : null;
        if (mode is { } chosen)
        {
            Close();
            Choose(chosen);
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }

    /// <summary>
    /// Starts a reset of the tab's current branch to <paramref name="commitHash"/>: opens the mode prompt. Returns why
    /// nothing was started (a refusal that ran no git), or an empty string when the prompt is open.
    /// </summary>
    public string Begin(TabId tabId, string commitHash)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return string.Empty;
        }

        if (_busy.Contains(tabId))
        {
            return BusyNotice;
        }

        if (Refusal(tab) is { } refusal)
        {
            return refusal;
        }

        _tabId = tabId;
        _targetHash = commitHash;
        _branchName = tab.Head!.BranchName!;
        _isOpen = true;
        setMode(KeyMode.Insert);
        return string.Empty;
    }

    private static string ShortHash(string hash) => hash[..Math.Min(ShortHashLength, hash.Length)];

    private static ResetMode? ModeOf(string name) => name switch
    {
        "s" => ResetMode.Soft,
        "m" => ResetMode.Mixed,
        "h" => ResetMode.Hard,
        _ => null,
    };

    private static string OptionOf(ResetMode mode) => mode switch
    {
        ResetMode.Soft => "--soft",
        ResetMode.Hard => "--hard",
        _ => "--mixed",
    };

    private static string ModeName(ResetMode mode) => OptionOf(mode)[2..];

    private static string OperationName(RepositoryOperation operation) => operation switch
    {
        RepositoryOperation.CherryPick => "cherry-pick",
        RepositoryOperation.ApplyMailbox => "mailbox apply",
        _ => operation.ToString().ToLowerInvariant(),
    };

    private static bool IsTracked(StatusEntry entry) =>
        entry.Kind is StatusEntryKind.Ordinary or StatusEntryKind.Renamed or StatusEntryKind.Copied
            or StatusEntryKind.Unmerged;

    private static string UndoCommand(ResetMode mode, string oldHash) => mode == ResetMode.Hard
        ? $"git reset --hard {oldHash}"
        : $"git reset {OptionOf(mode)} {oldHash}";

    private bool TryHandleOpenPrompt(ActionId action)
    {
        if (action == ActionIds.Cancel)
        {
            Close();
        }
        else if (action != ActionIds.InputAccept)
        {
            return false;
        }

        redraw();
        return true;
    }

    private void Close()
    {
        _isOpen = false;
        setMode(KeyMode.Normal);
    }

    // Why a reset must not start in this tab, or null. Reads the operation files, which is a few file checks.
    private string? Refusal(RepositoryState tab)
    {
        if (tab.Head is not { Kind: HeadKind.Branch, BranchName: not null })
        {
            return tab.Head?.Kind == HeadKind.Unborn
                ? "The branch has no commits yet: nothing to reset"
                : "HEAD is detached: switch to a branch first";
        }

        var operation = operations.Detect(tab.Location);
        return operation == RepositoryOperation.None
            ? null
            : $"A {OperationName(operation)} is in progress: finish or abort the {OperationName(operation)} first";
    }

    private void Choose(ResetMode mode)
    {
        if (host.Workspace.FindTab(_tabId) is not { } tab || !_busy.Add(tab.Id))
        {
            Notice = BusyNotice;
            return;
        }

        var target = new ResetTarget(tab.Id, _targetHash, _branchName, mode);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => ReadAndDecideAsync(tab, target, token), CancellationToken.None);
    }

    // Soft needs no facts; mixed and hard read the status to name what the reset drops.
    private async Task ReadAndDecideAsync(RepositoryState tab, ResetTarget target, CancellationToken token)
    {
        try
        {
            IReadOnlyList<StatusEntry> entries = [];
            if (target.Mode != ResetMode.Soft)
            {
                var snapshot = await statusReader
                    .ReadStatusAsync(tab.Location, false, UntrackedFilesMode.Normal, token)
                    .ConfigureAwait(false);
                entries = snapshot.Entries.Where(IsTracked).ToList();
            }

            postToUi(() => Decide(target, entries));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _busy.Remove(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not read the status, nothing was reset: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, false));
        }
    }

    private void Decide(ResetTarget target, IReadOnlyList<StatusEntry> changes)
    {
        _busy.Remove(target.TabId);
        if (host.Workspace.FindTab(target.TabId) is not { } tab)
        {
            return;
        }

        // The state may have changed while the status was read.
        if (Refusal(tab) is { } refusal)
        {
            statuses.Warning(refusal);
            return;
        }

        var prompt = target.Mode switch
        {
            ResetMode.Hard => HardPrompt(tab, target, changes),
            ResetMode.Mixed when changes.Any(entry => entry.IsStaged) => MixedPrompt(tab, target, changes),
            _ => null,
        };
        if (prompt is null)
        {
            Start(tab, target);
        }
        else if (!confirm(prompt, () => Start(tab.Id, target)))
        {
            statuses.Warning(TooSmallNotice);
        }

        redraw();
    }

    private static ConfirmPrompt HardPrompt(RepositoryState tab, ResetTarget target, IReadOnlyList<StatusEntry> changes)
    {
        var oldHash = tab.Head!.CommitHash!;
        List<string> lines =
        [
            $"Reset {target.BranchName} to {ShortHash(target.CommitHash)} with --hard: " +
            "HEAD, the index and the tracked files move to that commit.",
        ];
        if (changes.Count == 0)
        {
            lines.Add("There are no uncommitted changes to lose.");
        }
        else
        {
            lines.Add($"{OperationText.Plural(changes.Count, "file")} with uncommitted changes will be overwritten:");
            lines.AddRange(changes.Take(ListedFiles).Select(entry => $"  {entry.Path}"));
            if (changes.Count > ListedFiles)
            {
                lines.Add($"  and {changes.Count - ListedFiles} more");
            }
        }

        lines.Add("Untracked files are not touched.");
        return new ConfirmPrompt(
            "Hard reset",
            lines,
            $"Undo for the commits: {UndoCommand(ResetMode.Hard, oldHash)} (or ORIG_HEAD, the reflog). " +
            "Uncommitted changes are never in the reflog and cannot be restored.",
            "Reset hard");
    }

    private static ConfirmPrompt MixedPrompt(RepositoryState tab, ResetTarget target, IReadOnlyList<StatusEntry> changes)
    {
        var oldHash = tab.Head!.CommitHash!;
        var staged = changes.Count(entry => entry.IsStaged);
        var partial = changes.Count(entry => entry is { IsStaged: true, IsUnstaged: true });
        var detail = partial == 0 ? string.Empty : $" ({partial} partially staged)";
        return new ConfirmPrompt(
            "Mixed reset",
            [
                $"Reset {target.BranchName} to {ShortHash(target.CommitHash)} with --mixed: the index is cleared; " +
                "the files in the working tree stay.",
                $"{OperationText.Plural(staged, "file")} lose{(staged == 1 ? "s" : string.Empty)} the staged state{detail}.",
            ],
            $"The staged state cannot be recovered from the reflog. Undo for the commits: " +
            $"{UndoCommand(ResetMode.Mixed, oldHash)} (or ORIG_HEAD, the reflog).",
            "Reset mixed");
    }

    private void Start(TabId tabId, ResetTarget target)
    {
        if (host.Workspace.FindTab(tabId) is { } tab)
        {
            Start(tab, target);
        }
    }

    private void Start(RepositoryState tab, ResetTarget target)
    {
        if (!_busy.Add(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was reset");
            return;
        }

        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => ResetAsync(tab, target, token), CancellationToken.None);
    }

    private async Task ResetAsync(RepositoryState tab, ResetTarget target, CancellationToken token)
    {
        try
        {
            var oldHash = await writer
                .ResetAsync(tab.Location, target.Mode, target.CommitHash, token)
                .ConfigureAwait(false);
            var done = $"Reset {target.BranchName} to {ShortHash(target.CommitHash)} ({ModeName(target.Mode)}), " +
                $"was {ShortHash(oldHash)}; undo: {UndoCommand(target.Mode, oldHash)} (or ORIG_HEAD, the reflog)";
            postToUi(() => Finish(tab.Id, done, StatusSeverity.Info, true));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _busy.Remove(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not reset: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, true));
        }
    }

    private void Finish(TabId tabId, string text, StatusSeverity severity, bool refreshTab)
    {
        _busy.Remove(tabId);
        statuses.Post(text, severity);
        if (refreshTab)
        {
            refresh(tabId);
        }
    }
}
