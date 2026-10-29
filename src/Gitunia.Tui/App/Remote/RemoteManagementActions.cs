using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

// Core.Overlays has a FuzzyMatcher too (ConfirmPrompt lives there); the remote picker uses the Search one.
using RemoteMatcher = Gitunia.Core.Search.FuzzyMatcher;

namespace Gitunia.Tui.App.Remote;

/// <summary>
/// Add, remove, rename and re-point remotes (ux.md, workflow 5, "Managing remotes"). Names and URLs are typed in
/// prompt popups, the remote is chosen in a type-to-filter prompt (or taken from a selected remote branch row), and the
/// removal goes through the Confirm dialog, which names the remote-tracking branches that disappear. Nothing is
/// fetched. Everything runs off the UI thread, one operation per tab, and the tab is refreshed afterwards so the
/// sidebar and the graph labels follow. What an action acts on is fixed when its prompt opens.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="manager">Runs the git commands.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="refresh">Reads a tab again (refs, graph, status); false when it cannot.</param>
/// <param name="setMode">Switches the keymap mode (Insert while a prompt is open).</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
/// <param name="remoteOperationRuns">Whether a fetch, pull or push runs on the tab; remotes stay then.</param>
public sealed class RemoteManagementActions(
    WorkspaceHost host,
    IRemoteManager manager,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<TabId, bool> refresh,
    Action<KeyMode> setMode,
    Action redraw,
    Func<BranchSelection> selection,
    Func<TabId, bool> remoteOperationRuns) : IPromptHandler
{
    private const int ListedBranches = 5;
    private const string BusyNotice = "Busy: the last remote change is still running";
    private const string FetchRunsNotice = "Busy: a fetch, pull or push of this tab is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";
    private const string NoRemoteNotice = "There is no remote: add one first";
    private const char RemoteSeparator = '/';

    private readonly HashSet<TabId> _busy = [];
    private RemotePromptKind _kind;
    private RemotePromptKind _afterPick;
    private TabId _tabId;
    private string _text = string.Empty;
    private string _newName = string.Empty;
    private RemoteInfo _target = new(string.Empty, string.Empty);
    private IReadOnlyList<RemoteInfo> _remotes = [];
    private readonly FilterPicker<RemoteInfo> _picker = new();

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup
    {
        get
        {
            if (_kind == RemotePromptKind.None || host.Workspace.ActiveTabId != _tabId)
            {
                return null;
            }

            return _kind switch
            {
                RemotePromptKind.AddName => PromptPopup.ForText("Add remote", "Name of the new remote", _text, "next"),
                RemotePromptKind.AddUrl => PromptPopup.ForText("Add remote", $"URL of {_newName}", _text, "add"),
                RemotePromptKind.RenameName => PromptPopup.ForText(
                    "Rename remote", $"New name for {_target.Name}", _text, "rename"),
                RemotePromptKind.UrlText => PromptPopup.ForText(
                    "Set remote URL", $"New URL of {_target.Name}", _text, "set"),
                RemotePromptKind.RemovePick => PickerPopup("Remove remote", "Remote to remove"),
                RemotePromptKind.RenamePick => PickerPopup("Rename remote", "Remote to rename"),
                _ => PickerPopup("Set remote URL", "Remote to change"),
            };
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_kind != RemotePromptKind.None && host.Workspace.ActiveTabId != _tabId)
        {
            // The prompt belongs to the tab it was opened in; leaving that tab drops it.
            Close();
        }

        if (_kind != RemotePromptKind.None)
        {
            return TryHandleOpenPrompt(action);
        }

        if (action != ActionIds.RemoteAdd && action != ActionIds.RemoteRemove
            && action != ActionIds.RemoteRename && action != ActionIds.RemoteSetUrl)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is { } tab)
        {
            Notice = BeginOperation(tab) ?? Begin(tab, action);
            redraw();
        }

        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (_kind == RemotePromptKind.None || host.Workspace.ActiveTabId != _tabId)
        {
            return false;
        }

        var normalized = key.Normalize();
        var isPicker = IsPicker(_kind);
        if (isPicker && PickerKeys.IsMove(normalized, out var step))
        {
            _picker.Move(step);
        }
        else if (PromptTextEditor.Apply(_text, key) is { } edited && edited != _text)
        {
            _text = edited;
            if (isPicker)
            {
                RefreshCandidates();
            }
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }

    private static bool IsPicker(RemotePromptKind kind) =>
        kind is RemotePromptKind.RemovePick or RemotePromptKind.RenamePick or RemotePromptKind.UrlPick;

    // The remote a selected remote branch row belongs to; the longest name wins when one remote name prefixes another.
    private static RemoteInfo? RemoteOfBranch(IReadOnlyList<RemoteInfo> remotes, BranchSelection chosen)
    {
        if (chosen.RowKind != SidebarRowKind.RemoteBranch || chosen.Name is not { } name)
        {
            return null;
        }

        return remotes
            .Where(remote => name.StartsWith($"{remote.Name}{RemoteSeparator}", StringComparison.Ordinal))
            .OrderByDescending(remote => remote.Name.Length)
            .FirstOrDefault();
    }

    // The remote-tracking branches of a remote, not those of a remote whose name extends it (a and a/b).
    private static List<string> TrackingBranches(
        RepositoryState tab,
        IReadOnlyList<RemoteInfo> remotes,
        RemoteInfo remote)
    {
        var prefix = $"{remote.Name}{RemoteSeparator}";
        var longer = remotes
            .Where(other => other.Name.Length > remote.Name.Length
                && other.Name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(other => $"{other.Name}{RemoteSeparator}")
            .ToList();
        return
        [
            .. tab.Refs
                .Where(reference => reference.Kind == RefKind.RemoteBranch && reference.SymbolicTarget is null)
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)
                    && !longer.Any(other => name.StartsWith(other, StringComparison.Ordinal)))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string BranchList(List<string> branches)
    {
        var shown = string.Join(", ", branches.Take(ListedBranches));
        return branches.Count > ListedBranches ? $"{shown} and {branches.Count - ListedBranches} more" : shown;
    }

    private bool TryHandleOpenPrompt(ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            Notice = Accept();
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

    private string? BeginOperation(RepositoryState tab)
    {
        if (_busy.Contains(tab.Id))
        {
            return BusyNotice;
        }

        if (remoteOperationRuns(tab.Id))
        {
            return FetchRunsNotice;
        }

        return null;
    }

    private string Begin(RepositoryState tab, ActionId action)
    {
        if (action == ActionIds.RemoteAdd)
        {
            // The existing names are read first so a duplicate is refused before git runs.
            return LoadRemotes(tab, RemotePromptKind.AddName);
        }

        return LoadRemotes(tab, PickKindOf(action));
    }

    private static RemotePromptKind PickKindOf(ActionId action)
    {
        if (action == ActionIds.RemoteRemove)
        {
            return RemotePromptKind.RemovePick;
        }

        if (action == ActionIds.RemoteRename)
        {
            return RemotePromptKind.RenamePick;
        }

        return RemotePromptKind.UrlPick;
    }

    private void Close()
    {
        _kind = RemotePromptKind.None;
        setMode(KeyMode.Normal);
    }

    private void Open(RemotePromptKind kind, RepositoryState tab, string text)
    {
        _kind = kind;
        _tabId = tab.Id;
        _text = text;
        setMode(KeyMode.Insert);
    }

    private string LoadRemotes(RepositoryState tab, RemotePromptKind first)
    {
        _busy.Add(tab.Id);
        var token = cancellationOf(tab.Id);
        var chosen = selection();
        _ = Task.Run(() => ReadRemotesAsync(tab, first, chosen, token), CancellationToken.None);
        return string.Empty;
    }

    private async Task ReadRemotesAsync(
        RepositoryState tab,
        RemotePromptKind first,
        BranchSelection chosen,
        CancellationToken token)
    {
        try
        {
            var remotes = await manager.ListAsync(tab.Location, token).ConfigureAwait(false);
            postToUi(() => OpenFirstPrompt(tab, first, chosen, remotes));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _busy.Remove(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not list the remotes: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, false));
        }
    }

    private void OpenFirstPrompt(
        RepositoryState tab,
        RemotePromptKind first,
        BranchSelection chosen,
        IReadOnlyList<RemoteInfo> remotes)
    {
        _busy.Remove(tab.Id);
        if (host.Workspace.ActiveTabId != tab.Id || _kind != RemotePromptKind.None)
        {
            return;
        }

        _remotes = remotes;
        if (first == RemotePromptKind.AddName)
        {
            Open(first, tab, string.Empty);
        }
        else if (remotes.Count == 0)
        {
            statuses.Warning(NoRemoteNotice);
        }
        else if (RemoteOfBranch(remotes, chosen) is { } selected)
        {
            _afterPick = first;
            AfterPick(tab, selected);
        }
        else
        {
            _afterPick = first;
            Open(first, tab, string.Empty);
            RefreshCandidates();
        }

        redraw();
    }

    private void RefreshCandidates() =>
        _picker.Filter(_remotes, _text, remote => remote.Name, RemoteMatcher.Score);

    private PromptPopup PickerPopup(string title, string context) =>
        _picker.ToPopup(title, context, _text, "choose", remote => remote.Name, "No matching remote");

    private string Accept()
    {
        if (host.Workspace.FindTab(_tabId) is not { } tab)
        {
            Close();
            return string.Empty;
        }

        var kind = _kind;
        if (IsPicker(kind))
        {
            return AcceptPick(tab);
        }

        var text = _text.Trim();
        switch (kind)
        {
            case RemotePromptKind.AddName:
                return AcceptAddName(tab, text);
            case RemotePromptKind.AddUrl:
                return AcceptAddUrl(tab, text);
            case RemotePromptKind.RenameName:
                return AcceptRename(tab, text);
            default:
                return AcceptUrl(tab, text);
        }
    }

    private string AcceptPick(RepositoryState tab)
    {
        if (!_picker.TryGetSelected(out var remote))
        {
            return "No matching remote";
        }

        Close();
        return AfterPick(tab, remote);
    }

    // The remote is fixed; remove asks, rename and set-url type what comes next.
    private string AfterPick(RepositoryState tab, RemoteInfo remote)
    {
        _target = remote;
        if (_afterPick == RemotePromptKind.RemovePick)
        {
            return AskRemove(tab, remote);
        }

        if (_afterPick == RemotePromptKind.RenamePick)
        {
            Open(RemotePromptKind.RenameName, tab, string.Empty);
        }
        else
        {
            Open(RemotePromptKind.UrlText, tab, remote.Url);
        }

        return string.Empty;
    }

    private string AcceptAddName(RepositoryState tab, string name)
    {
        if (name.Length == 0)
        {
            Close();
            return "No name: nothing was added";
        }

        if (_remotes.Any(remote => remote.Name == name))
        {
            Close();
            return $"Remote {name} already exists";
        }

        _newName = name;
        Open(RemotePromptKind.AddUrl, tab, string.Empty);
        return string.Empty;
    }

    private string AcceptAddUrl(RepositoryState tab, string url)
    {
        Close();
        if (url.Length == 0)
        {
            return "No URL: nothing was added";
        }

        var name = _newName;
        Start(tab, "Could not add the remote", async token =>
        {
            await manager.AddAsync(tab.Location, name, url, token).ConfigureAwait(false);
            return $"Added remote {name} ({url}); run fetch to get its branches";
        });
        return string.Empty;
    }

    private string AcceptRename(RepositoryState tab, string newName)
    {
        Close();
        var old = _target.Name;
        if (newName.Length == 0 || newName == old)
        {
            return "Same or empty name: nothing was renamed";
        }

        if (_remotes.Any(remote => remote.Name == newName))
        {
            return $"Remote {newName} already exists";
        }

        Start(tab, "Could not rename the remote", async token =>
        {
            await manager.RenameAsync(tab.Location, old, newName, token).ConfigureAwait(false);
            return $"Renamed remote {old} to {newName}";
        });
        return string.Empty;
    }

    private string AcceptUrl(RepositoryState tab, string url)
    {
        Close();
        var name = _target.Name;
        var old = _target.Url;
        if (url.Length == 0 || url == old)
        {
            return "Same or empty URL: nothing was changed";
        }

        Start(tab, "Could not set the URL", async token =>
        {
            await manager.SetUrlAsync(tab.Location, name, url, token).ConfigureAwait(false);
            return $"Set the URL of {name} to {url} (was {old})";
        });
        return string.Empty;
    }

    private string AskRemove(RepositoryState tab, RemoteInfo remote)
    {
        var branches = TrackingBranches(tab, _remotes, remote);
        var lines = new List<string> { $"Remove the remote {remote.Name} ({remote.Url})?" };
        if (branches.Count == 0)
        {
            lines.Add("It has no remote-tracking branches.");
        }
        else
        {
            var gone = $"{branches.Count} remote-tracking branches disappear";
            if (branches.Count == 1)
            {
                gone = "1 remote-tracking branch disappears";
            }

            lines.Add($"{gone}: {BranchList(branches)}.");
        }

        lines.Add("Branches that track it lose their upstream. The remote itself is not touched.");
        var prompt = new ConfirmPrompt(
            "Remove remote",
            lines,
            $"Undo: git remote add {remote.Name} {remote.Url}, then fetch it",
            "Remove remote");
        return confirm(prompt, () => StartRemove(tab.Id, remote)) ? string.Empty : TooSmallNotice;
    }

    private void StartRemove(TabId tabId, RemoteInfo remote)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || _busy.Contains(tabId) || remoteOperationRuns(tabId))
        {
            statuses.Warning($"{BusyNotice}; nothing was removed");
            return;
        }

        Start(tab, "Could not remove the remote", async token =>
        {
            await manager.RemoveAsync(tab.Location, remote.Name, token).ConfigureAwait(false);
            return $"Removed remote {remote.Name} (restore: git remote add {remote.Name} {remote.Url}, then fetch)";
        });
    }

    // Own busy set, not the shared operation gate: remote config changes never take the index lock.
    private void Start(RepositoryState tab, string failure, Func<CancellationToken, Task<string>> work)
    {
        _busy.Add(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab.Id, failure, work, token), CancellationToken.None);
    }

    private async Task RunAsync(
        TabId tabId,
        string failure,
        Func<CancellationToken, Task<string>> work,
        CancellationToken token)
    {
        try
        {
            var done = await work(token).ConfigureAwait(false);
            postToUi(() => Finish(tabId, done, StatusSeverity.Info, true));
        }
        catch (OperationCanceledException)
        {
            // Only the closing of the tab cancels; there is nothing left to refresh or report.
            postToUi(() => _busy.Remove(tabId));
        }
        catch (Exception exception)
        {
            var text = $"{failure}: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tabId, text, StatusSeverity.Error, true));
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
