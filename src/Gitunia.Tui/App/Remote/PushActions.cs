using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.Input;
// Core.Overlays has a FuzzyMatcher too (ConfirmPrompt lives there); the pickers use the Search one.
using PickerMatcher = Gitunia.Core.Search.FuzzyMatcher;

namespace Gitunia.Tui.App.Remote;

/// <summary>
/// The push cases that need a question (ux-workflows.md, workflow 5): the first push of a branch without an upstream asks for
/// the remote (<c>origin</c> first) and sets the upstream; a force push is only ever a lease and goes through a
/// Confirm that names the remote branch it overwrites; deleting a branch on a remote asks for the remote, then the
/// branch, then confirms. The push itself runs through <see cref="RemoteActions"/> (progress, cancel, one remote
/// operation per tab); the delete is a short call of its own. A plain push of a branch with an upstream is not
/// handled here. What an action acts on is fixed when its prompt opens.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="remote">Runs the pushes as the tab's remote operation.</param>
/// <param name="writer">Deletes a branch on a remote.</param>
/// <param name="remotesOf">Lists the configured remotes of a repository.</param>
/// <param name="isAncestor">Whether the first commit is in the history of the second (a first push is only offered a
/// force when the local branch is not just behind the remote one).</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="busy">Marks the tab while an operation of this kind runs (one at a time; tab eviction waits for it).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="refresh">Reads a tab again; false when it cannot.</param>
/// <param name="setMode">Switches the keymap mode (Insert while a prompt is open).</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="selection">What the cursor is on: a remote branch row is deleted without the pickers.</param>
public sealed class PushActions(
    Dispatcher host,
    RemoteActions remote,
    IRemoteWriter writer,
    Func<RepositoryLocation, CancellationToken, Task<IReadOnlyList<string>>> remotesOf,
    Func<RepositoryLocation, string, string, CancellationToken, Task<bool>> isAncestor,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    TabOperationGate busy,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<TabId, bool> refresh,
    Action<KeyMode> setMode,
    Action redraw,
    Func<BranchSelection> selection) : IPromptHandler
{
    private const string DefaultRemote = "origin";
    private const string BusyNotice = "Busy: the last push operation is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";
    private const string NoRemoteNotice = "There is no remote: add one first";

    private PushPromptKind _kind;
    private TabId _tabId;
    private string _text = string.Empty;
    private string _branch = string.Empty;
    private string _chosenRemote = string.Empty;
    private IReadOnlyList<RemoteBranchRef> _branches = [];
    private IReadOnlyList<string> _options = [];
    private readonly FilterPicker<string> _picker = new();
    private string? _directName;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup
    {
        get
        {
            if (_kind == PushPromptKind.None || host.Workspace.ActiveTabId != _tabId)
            {
                return null;
            }

            return _kind switch
            {
                PushPromptKind.FirstPushRemote => FilterPopup("Push and set upstream", $"Remote to push {_branch} to"),
                PushPromptKind.DeleteRemote => FilterPopup("Delete remote branch", "Remote to delete a branch from"),
                _ => FilterPopup("Delete remote branch", $"Branch to delete on {_chosenRemote}"),
            };
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_kind != PushPromptKind.None && host.Workspace.ActiveTabId != _tabId)
        {
            // The prompt belongs to the tab it was opened in; leaving that tab drops it.
            Close();
        }

        if (_kind != PushPromptKind.None)
        {
            return TryHandleOpenPrompt(action);
        }

        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        if (action == ActionIds.RemotePush)
        {
            // A branch that already has an upstream is the plain push of RemoteActions.
            if (CurrentBranchOf(tab) is not { } current || HasUpstream(current) || remote.IsRunning(tab.Id))
            {
                return false;
            }

            Notice = busy.IsBusy(tab.Id) ? BusyNotice : LoadRemotes(tab, action, current);
        }
        else if (action == ActionIds.RemotePushForce)
        {
            Notice = busy.IsBusy(tab.Id) ? BusyNotice : LoadRemotes(tab, action, CurrentBranchOf(tab));
        }
        else if (action == ActionIds.RemoteDeleteBranch || action == ActionIds.BranchDelete
            && RemoteDeleteTarget.IsRemoteOnly(tab, selection()))
        {
            // When the cursor names one remote branch (its row, or the one on the graph commit) there is no picker.
            var named = RemoteDeleteTarget.Names(tab, selection());
            _directName = named.Count == 1 ? named[0] : null;
            Notice = busy.IsBusy(tab.Id) ? BusyNotice : LoadRemotes(tab, action, null);
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
        if (_kind == PushPromptKind.None || host.Workspace.ActiveTabId != _tabId)
        {
            return false;
        }

        var normalized = key.Normalize();
        if (PickerKeys.IsMove(normalized, out var step))
        {
            _picker.Move(step);
        }
        else if (PromptTextEditor.Apply(_text, key) is { } edited && edited != _text)
        {
            _text = edited;
            RefreshCandidates();
        }

        redraw();
        return true;
    }

    /// <summary>
    /// A plain push of the tab was rejected as a non-fast-forward (the branch was rewritten or diverged): offers the
    /// force push Confirm of <c>&lt;space&gt;Rf</c> right away (#746). Nothing happens when the tab is not active, a
    /// prompt is open or the branch cannot be force pushed; the push failure stays in the status bar.
    /// </summary>
    public void OfferForce(TabId tabId, string pushedBranch)
    {
        // A branch that is only behind its upstream is rejected too, but the answer there is a pull, not a force.
        if (_kind != PushPromptKind.None || busy.IsBusy(tabId) || host.Workspace.ActiveTabId != tabId
            || host.Workspace.ActiveTab is not { } tab || CurrentBranchOf(tab) is not { } current
            || !HasUpstream(current) || current.Name != pushedBranch || current.Ahead is not > 0)
        {
            return;
        }

        LoadRemotes(tab, ActionIds.RemotePushForce, current);
    }

    /// <summary>
    /// The first push of a branch (<c>--set-upstream</c>) was rejected as a non-fast-forward: the remote already has a
    /// branch of that name with other commits. Offers the lease force push that also sets the upstream, when the
    /// remote branch is known locally (the lease needs its tip); otherwise the status says to fetch first.
    /// </summary>
    public void OfferForceFirstPush(TabId tabId, string pushedBranch, string remoteName)
    {
        if (_kind != PushPromptKind.None || busy.IsBusy(tabId) || host.Workspace.ActiveTabId != tabId
            || host.Workspace.ActiveTab is not { } tab || CurrentBranchOf(tab) is not { } current
            || current.Name != pushedBranch)
        {
            return;
        }

        var tracking = tab.Refs.FirstOrDefault(
            reference => reference.Kind == RefKind.RemoteBranch
                && reference.SymbolicTarget is null
                && reference.Name == $"{remoteName}/{pushedBranch}");
        if (tracking is null)
        {
            statuses.Warning($"{remoteName}/{pushedBranch} is not known here: fetch first, then force push (<space>rP)");
            return;
        }

        // Only a branch with commits of its own is worth a force: one that is just behind needs a pull or rebase.
        busy.Begin(tabId);
        var token = cancellationOf(tabId);
        var target = new RemoteBranchName(remoteName, pushedBranch);
        _ = Task.Run(() => CheckAheadAsync(tab, current, target, tracking, token), CancellationToken.None);
    }

    public void OnGraphChanged()
    {
    }

    private static GitRef? CurrentBranchOf(RepositoryState tab) =>
        tab.Refs.FirstOrDefault(reference => reference.Kind == RefKind.LocalBranch && reference.IsCurrent);

    private static bool HasUpstream(GitRef branch) => branch.UpstreamFullName is not null && !branch.IsUpstreamGone;


    private async Task CheckAheadAsync(
        RepositoryState tab,
        GitRef current,
        RemoteBranchName target,
        GitRef tracking,
        CancellationToken token)
    {
        try
        {
            var behind = await isAncestor(tab.Location, current.CommitHash, tracking.CommitHash, token)
                .ConfigureAwait(false);
            postToUi(() => OnAheadChecked(tab, current, target, tracking, behind));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => busy.End(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not compare {current.Name} with {target.Remote}/{target.Branch}: "
                + RemoteFailureText.Reason(exception);
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, false));
        }
    }

    private void OnAheadChecked(
        RepositoryState tab,
        GitRef current,
        RemoteBranchName target,
        GitRef tracking,
        bool behind)
    {
        busy.End(tab.Id);
        if (_kind != PushPromptKind.None || host.Workspace.ActiveTabId != tab.Id
            || host.Workspace.ActiveTab is not { } active || CurrentBranchOf(active) is not { } now || now.Name != current.Name || now.CommitHash != current.CommitHash)
        {
            return;
        }

        if (behind)
        {
            statuses.Warning(
                $"{current.Name} is behind {target.Remote}/{target.Branch}: pull or rebase it, no force push offered");
        }
        else
        {
            ConfirmForce(tab, current, target, tracking, true);
        }

        redraw();
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

    private void Close()
    {
        _kind = PushPromptKind.None;
        setMode(KeyMode.Normal);
    }

    private void Open(PushPromptKind kind, RepositoryState tab, IReadOnlyList<string> options)
    {
        _kind = kind;
        _tabId = tab.Id;
        _text = string.Empty;
        _options = options;
        setMode(KeyMode.Insert);
        RefreshCandidates();
    }

    private string LoadRemotes(RepositoryState tab, ActionId action, GitRef? current)
    {
        if (action == ActionIds.RemotePushForce && (current is null || !HasUpstream(current)))
        {
            if (current is null)
            {
                return "No branch is checked out: nothing to force push";
            }

            return $"{current.Name} has no upstream: push it first";
        }

        busy.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => ReadRemotesAsync(tab, action, current, token), CancellationToken.None);
        return string.Empty;
    }

    private async Task ReadRemotesAsync(RepositoryState tab, ActionId action, GitRef? current, CancellationToken token)
    {
        try
        {
            var remotes = await remotesOf(tab.Location, token).ConfigureAwait(false);
            postToUi(() => OnRemotesRead(tab, action, current, remotes));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => busy.End(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not list the remotes: {RemoteFailureText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, false));
        }
    }

    private void OnRemotesRead(RepositoryState tab, ActionId action, GitRef? current, IReadOnlyList<string> remotes)
    {
        busy.End(tab.Id);
        if (host.Workspace.ActiveTabId != tab.Id || _kind != PushPromptKind.None)
        {
            return;
        }

        if (remotes.Count == 0)
        {
            statuses.Warning(NoRemoteNotice);
        }
        else if (action == ActionIds.RemotePush)
        {
            _branch = current!.Name;
            if (remotes is [var only])
            {
                // One remote: asking for it is a step without a choice.
                if (StartFirstPush(tab, only) is { Length: > 0 } notice)
                {
                    statuses.Warning(notice);
                }

                redraw();
                return;
            }

            var ordered = remotes.OrderByDescending(name => name == DefaultRemote).ToList();
            Open(PushPromptKind.FirstPushRemote, tab, ordered);
        }
        else if (action == ActionIds.RemotePushForce)
        {
            AskForce(tab, current!, remotes);
        }
        else
        {
            OpenDeleteRemote(tab, remotes);
        }

        redraw();
    }

    private void AskForce(RepositoryState tab, GitRef branch, IReadOnlyList<string> remotes)
    {
        var upstream = tab.Refs.FirstOrDefault(reference => reference.FullName == branch.UpstreamFullName);
        var target = upstream is null ? null : RemoteBranchName.Split(upstream.Name, remotes);
        if (upstream is null || target is null)
        {
            statuses.Warning($"The upstream of {branch.Name} is not a known remote branch: fetch first");
            return;
        }

        ConfirmForce(tab, branch, target, upstream, false);
    }

    private void ConfirmForce(
        RepositoryState tab,
        GitRef branch,
        RemoteBranchName target,
        GitRef upstream,
        bool setUpstream)
    {
        var name = $"{target.Remote}/{target.Branch}";
        var prompt = new ConfirmPrompt(
            "Force push",
            [
                $"Force push {branch.Name} to {name}, overwriting it (now at {ShortHash.Of(upstream.CommitHash)})?",
                "A lease: refused when the remote moved since your last fetch.",
                "Commits that are only on the remote branch are no longer on it afterwards.",
            ],
            $"Undo: git push --force-with-lease {target.Remote} {upstream.CommitHash}:{target.Branch}",
            "Force push");
        var lease = new PushLease(target.Branch, upstream.CommitHash);
        var request = new PushRequest(target.Remote, branch.Name, setUpstream, lease);
        var done = $"Force pushed {branch.Name} to {name} (was {ShortHash.Of(upstream.CommitHash)})";
        if (!confirm(prompt, () => StartForce(tab.Id, request, done)))
        {
            statuses.Warning(TooSmallNotice);
        }
    }

    private void StartForce(TabId tabId, PushRequest request, string done)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        if (remote.StartPush(tab, request, done) is { Length: > 0 } notice)
        {
            statuses.Warning($"{notice}; nothing was pushed");
        }

        redraw();
    }

    private void OpenDeleteRemote(RepositoryState tab, IReadOnlyList<string> remotes)
    {
        _branches = RemoteBranchesOf(tab, remotes);
        if (_directName is { } direct)
        {
            _directName = null;
            AskDeleteRow(tab, remotes, direct);
            return;
        }

        var withBranches = _branches.Select(branch => branch.Name.Remote).Distinct().ToList();
        if (withBranches.Count == 0)
        {
            statuses.Warning("No remote branches known: fetch first");
            return;
        }

        if (withBranches is [var only])
        {
            // One remote has branches: asking for it first is a step without a choice.
            _chosenRemote = only;
            var names = _branches.Where(branch => branch.Name.Remote == only).Select(branch => branch.Name.Branch);
            Open(PushPromptKind.DeleteBranch, tab, [.. names]);
            return;
        }

        Open(PushPromptKind.DeleteRemote, tab, withBranches);
    }

    private void AskDeleteRow(RepositoryState tab, IReadOnlyList<string> remotes, string rowName)
    {
        var name = RemoteBranchName.Split(rowName, remotes);
        if (name is null || !_branches.Any(item => item.Name == name))
        {
            statuses.Warning($"{rowName} is not a branch on a remote: nothing to delete");
            return;
        }

        _chosenRemote = name.Remote;
        if (AskDelete(tab, name.Branch) is { Length: > 0 } notice)
        {
            statuses.Warning(notice);
        }
    }

    private static List<RemoteBranchRef> RemoteBranchesOf(RepositoryState tab, IReadOnlyList<string> remotes)
    {
        var branches = new List<RemoteBranchRef>();
        var remoteRefs = tab.Refs.Where(item => item.Kind == RefKind.RemoteBranch && item.SymbolicTarget is null);
        foreach (var reference in remoteRefs)
        {
            if (RemoteBranchName.Split(reference.Name, remotes) is { } name)
            {
                branches.Add(new RemoteBranchRef(name, reference.CommitHash));
            }
        }

        return branches;
    }

    private string Accept()
    {
        if (host.Workspace.FindTab(_tabId) is not { } tab)
        {
            Close();
            return string.Empty;
        }

        if (!_picker.TryGetSelected(out var chosen))
        {
            return "No match";
        }

        var kind = _kind;
        Close();
        if (kind == PushPromptKind.FirstPushRemote)
        {
            return StartFirstPush(tab, chosen);
        }

        if (kind == PushPromptKind.DeleteRemote)
        {
            _chosenRemote = chosen;
            var names = _branches.Where(branch => branch.Name.Remote == chosen).Select(branch => branch.Name.Branch);
            Open(PushPromptKind.DeleteBranch, tab, [.. names]);
            return string.Empty;
        }

        return AskDelete(tab, chosen);
    }

    private string StartFirstPush(RepositoryState tab, string chosenRemote)
    {
        if (CurrentBranchOf(tab)?.Name != _branch)
        {
            return "The checked-out branch changed: nothing was pushed";
        }

        var request = new PushRequest(chosenRemote, _branch, true);
        return remote.StartPush(tab, request, $"Pushed {_branch} to {chosenRemote} and set it as its upstream");
    }

    private string AskDelete(RepositoryState tab, string branch)
    {
        var remoteName = _chosenRemote;
        var target = _branches.First(item => item.Name.Remote == remoteName && item.Name.Branch == branch);
        var prompt = new ConfirmPrompt(
            "Delete branch on remote",
            [
                $"Delete the branch {branch} on the remote {remoteName} (at {ShortHash.Of(target.CommitHash)})?",
                "Local branches are not touched.",
            ],
            $"Undo: git push {remoteName} {target.CommitHash}:{GitRefPrefixes.Heads}{branch}",
            "Delete on remote");
        return confirm(prompt, () => StartDelete(tab.Id, target)) ? string.Empty : TooSmallNotice;
    }

    private void StartDelete(TabId tabId, RemoteBranchRef target)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || busy.IsBusy(tabId))
        {
            statuses.Warning($"{BusyNotice}; nothing was deleted");
            return;
        }

        busy.Begin(tabId);
        var token = cancellationOf(tabId);
        _ = Task.Run(() => DeleteAsync(tab, target, token), CancellationToken.None);
    }

    private async Task DeleteAsync(RepositoryState tab, RemoteBranchRef target, CancellationToken token)
    {
        var name = target.Name;
        try
        {
            var tip = target.CommitHash;
            await writer
                .DeleteRemoteBranchAsync(tab.Location, name.Remote, name.Branch, tip, token)
                .ConfigureAwait(false);
            var restore = $"git push {name.Remote} {tip}:{GitRefPrefixes.Heads}{name.Branch}";
            var done = $"Deleted {name.Branch} on {name.Remote} (restore: {restore})";
            postToUi(() => Finish(tab.Id, done, StatusSeverity.Info, true));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => busy.End(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not delete the branch on the remote: {RemoteFailureText.Reason(exception)}";
            var offer = exception is GitCommandFailedException failed
                && RemoteAuthFailure.Matches(failed.StandardError);
            postToUi(() =>
            {
                Finish(tab.Id, failure, StatusSeverity.Error, true);
                if (offer)
                {
                    var retry = RemoteCommands.BuildDeleteBranch(name.Remote, name.Branch, target.CommitHash, false);
                    remote.OfferTerminalRetry(tab.Id, "Remote branch delete", retry, () => busy.IsBusy(tab.Id));
                }
            });
        }
    }

    private void Finish(TabId tabId, string text, StatusSeverity severity, bool refreshTab)
    {
        busy.End(tabId);
        statuses.Post(text, severity);
        if (refreshTab)
        {
            refresh(tabId);
        }
    }

    private void RefreshCandidates() =>
        _picker.Filter(_options, _text, option => option, PickerMatcher.Score);

    private PromptPopup FilterPopup(string title, string context) =>
        _picker.ToPopup(title, context, _text, "choose", option => option, "No match");

    private sealed record RemoteBranchRef(RemoteBranchName Name, string CommitHash);
}
