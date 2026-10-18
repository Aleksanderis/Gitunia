using System.Globalization;
using Gitunia.Core.Branches;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Branches;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Branches;

/// <summary>
/// Create, switch, rename, delete branches and set or unset their upstream (ux.md, workflow 3, "Branches").
/// A name is typed in a prompt popup,
/// a switch target is chosen in a type-to-filter prompt, and a delete goes through the Confirm dialog (a second one
/// when the branch holds commits that nothing else reaches). Refusals that need no git (the current branch, a branch
/// held by another worktree, a name in use) are decided from the tab refs and run nothing; everything else runs off
/// the UI thread, one operation per tab, and the tab is refreshed afterwards. What the action acts on is fixed when
/// the prompt opens.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs the git commands.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="refresh">Reads a tab again (refs, graph, status); false when it cannot.</param>
/// <param name="setMode">Switches the keymap mode (Insert while a prompt is open).</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
public sealed class BranchActions(
    WorkspaceHost host,
    IBranchWriter writer,
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
    private const string BusyNotice = "Busy: the last branch operation is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was deleted";
    private const string SelectNotice = "Select a local branch (sidebar) or a commit with one branch on it";

    private readonly HashSet<TabId> _busy = [];
    private BranchPromptKind _kind;
    private TabId _tabId;
    private string _text = string.Empty;
    private bool _checkout;
    private string _startPoint = string.Empty;
    private string _label = string.Empty;
    private string? _commitHash;
    private IReadOnlyList<SwitchCandidate> _candidates = [];
    private int _index;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup => _kind == BranchPromptKind.None || host.Workspace.ActiveTabId != _tabId
        ? null
        : _kind switch
        {
            BranchPromptKind.Create => PromptPopup.ForText(
                "New branch",
                $"At {_label}{(_checkout ? string.Empty : " (not checked out)")}",
                _text,
                "create"),
            BranchPromptKind.Rename => PromptPopup.ForText("Rename branch", _label, _text, "rename"),
            BranchPromptKind.Upstream => PromptPopup.ForFilter(
                "Set upstream", $"Upstream of {_label}", _text, UpstreamDetail(), "set"),
            _ => PromptPopup.ForFilter("Switch branch", "Branch to check out", _text, SwitchDetail(), "switch"),
        };

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_kind != BranchPromptKind.None && host.Workspace.ActiveTabId != _tabId)
        {
            // The prompt belongs to the tab it was opened in; leaving that tab drops it.
            Close();
        }

        if (_kind != BranchPromptKind.None)
        {
            return TryHandleOpenPrompt(action);
        }

        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        if (action == ActionIds.BranchCreate || action == ActionIds.BranchCreateOnly)
        {
            var checkout = action == ActionIds.BranchCreate;
            Notice = BeginOperation(tab) ?? (checkout ? OperationRefusal(tab) : null) ?? OpenCreate(tab, checkout);
        }
        else if (action == ActionIds.BranchSwitch)
        {
            Notice = BeginOperation(tab) ?? OperationRefusal(tab) ?? OpenSwitch(tab);
        }
        else if (action == ActionIds.BranchRename)
        {
            Notice = BeginOperation(tab) ?? OpenRename(tab);
        }
        else if (action == ActionIds.BranchDelete)
        {
            Notice = BeginOperation(tab) ?? AskDelete(tab);
        }
        else if (action == ActionIds.BranchSetUpstream)
        {
            Notice = BeginOperation(tab) ?? OpenUpstream(tab);
        }
        else if (action == ActionIds.BranchUnsetUpstream)
        {
            Notice = BeginOperation(tab) ?? UnsetUpstream(tab);
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
        if (_kind == BranchPromptKind.None || host.Workspace.ActiveTabId != _tabId)
        {
            return false;
        }

        var normalized = key.Normalize();
        if (_kind is BranchPromptKind.Switch or BranchPromptKind.Upstream && IsMove(normalized, out var step))
        {
            _index = _candidates.Count == 0 ? 0 : (_index + step + _candidates.Count) % _candidates.Count;
        }
        else if (PromptTextEditor.Apply(_text, key) is { } edited && edited != _text)
        {
            _text = edited;
            if (_kind is BranchPromptKind.Switch or BranchPromptKind.Upstream)
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

    /// <summary>
    /// Opens the new-branch prompt with <paramref name="commitHash"/> as the start point (the reflog view starts a
    /// branch at an entry). Returns why nothing opened, or an empty string when the prompt is open.
    /// </summary>
    public string BeginCreate(TabId tabId, string commitHash, bool checkout)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return string.Empty;
        }

        if ((BeginOperation(tab) ?? (checkout ? OperationRefusal(tab) : null)) is { } busy)
        {
            return busy;
        }

        _startPoint = commitHash;
        _label = ShortHash(commitHash);
        _checkout = checkout;
        Open(BranchPromptKind.Create, tab, string.Empty);
        redraw();
        return string.Empty;
    }

    /// <summary>
    /// Checks out <paramref name="commitHash"/> with a detached HEAD (the reflog view checks out an entry). Returns why
    /// nothing started, or an empty string when the checkout runs in the background.
    /// </summary>
    public string CheckoutDetached(TabId tabId, string commitHash)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return string.Empty;
        }

        if ((BeginOperation(tab) ?? OperationRefusal(tab)) is { } busy)
        {
            return busy;
        }

        Start(tab, "Could not check out", async token =>
        {
            await writer.SwitchDetachedAsync(tab.Location, commitHash, token).ConfigureAwait(false);
            return $"HEAD is detached at {ShortHash(commitHash)}";
        }, CommitOf(tab, commitHash));
        return string.Empty;
    }

    private static bool IsMove(KeyEvent key, out int step)
    {
        step = 0;
        if (key is { Name: KeyNames.Up, Modifiers: KeyModifiers.None })
        {
            step = -1;
        }
        else if (key is { Name: KeyNames.Down or KeyNames.Tab, Modifiers: KeyModifiers.None })
        {
            step = 1;
        }

        return step != 0;
    }

    private static string ShortHash(string hash) => hash[..Math.Min(ShortHashLength, hash.Length)];

    private bool TryHandleOpenPrompt(ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            var kind = _kind;
            var tabId = _tabId;
            Close();
            Notice = Accept(kind, tabId);
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

    // A checkout must not run while a merge, rebase, cherry-pick or revert is unfinished. No git runs.
    private static string? OperationRefusal(RepositoryState tab)
    {
        if (tab.Operation is not { } operation)
        {
            return null;
        }

        var name = OperationBannerBuilder.NameOf(operation.Kind);
        return $"A {name} is in progress: finish or abort the {name} first (<space>gc continue, <space>ga abort)";
    }

    private string? BeginOperation(RepositoryState tab) => _busy.Contains(tab.Id) ? BusyNotice : null;

    private void Close()
    {
        _kind = BranchPromptKind.None;
        setMode(KeyMode.Normal);
    }

    private void Open(BranchPromptKind kind, RepositoryState tab, string text)
    {
        _kind = kind;
        _tabId = tab.Id;
        _text = text;
        setMode(KeyMode.Insert);
    }

    private string OpenCreate(RepositoryState tab, bool checkout)
    {
        var chosen = selection();
        if (chosen.RowKind is SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch && chosen.Name is { } name)
        {
            // The full ref name, so a tag called like the branch cannot be picked instead.
            _startPoint = chosen.RowKind == SidebarRowKind.LocalBranch
                ? $"{GitRefPrefixes.Heads}{name}"
                : $"{GitRefPrefixes.Remotes}{name}";
            _label = name;
        }
        else if (chosen.CommitHash is { } hash)
        {
            _startPoint = hash;
            _label = ShortHash(hash);
        }
        else
        {
            return "Select a commit or a branch first";
        }

        _checkout = checkout;
        Open(BranchPromptKind.Create, tab, string.Empty);
        return string.Empty;
    }

    private string OpenRename(RepositoryState tab)
    {
        if (ResolveLocalBranch(tab, out var branch) is { } refusal)
        {
            return refusal;
        }

        _label = branch!.Name;
        Open(BranchPromptKind.Rename, tab, branch.Name);
        return string.Empty;
    }

    private string OpenSwitch(RepositoryState tab)
    {
        var chosen = selection();
        _commitHash = chosen.RowKind is null ? chosen.CommitHash : null;
        Open(BranchPromptKind.Switch, tab, string.Empty);
        RefreshCandidates();
        return string.Empty;
    }

    private string OpenUpstream(RepositoryState tab)
    {
        if (ResolveLocalBranch(tab, out var branch) is { } refusal)
        {
            return refusal;
        }

        if (!tab.Refs.Any(reference => reference.Kind == RefKind.RemoteBranch && reference.SymbolicTarget is null))
        {
            return "There is no remote branch to track: add or fetch a remote first";
        }

        _label = branch!.Name;
        _commitHash = null;
        Open(BranchPromptKind.Upstream, tab, string.Empty);
        RefreshCandidates();
        return string.Empty;
    }

    private string UnsetUpstream(RepositoryState tab)
    {
        if (ResolveLocalBranch(tab, out var branch) is { } refusal)
        {
            return refusal;
        }

        if (branch!.UpstreamFullName is not { } upstream)
        {
            return $"{branch.Name} has no upstream";
        }

        var name = branch.Name;
        var shown = upstream.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal)
            ? upstream[GitRefPrefixes.Remotes.Length..]
            : upstream.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal)
                ? upstream[GitRefPrefixes.Heads.Length..]
                : upstream;
        Start(tab, "Could not unset the upstream", async token =>
        {
            await writer.UnsetUpstreamAsync(tab.Location, name, token).ConfigureAwait(false);
            return $"{name} no longer tracks {shown} (restore: git branch --set-upstream-to={shown} {name})";
        });
        return string.Empty;
    }

    private string UpstreamDetail() => CandidateDetail("No matching remote branch");

    private string AcceptUpstream(RepositoryState tab)
    {
        if (_index >= _candidates.Count)
        {
            return "No matching remote branch";
        }

        var name = _label;
        var upstream = _candidates[_index].Target;
        Start(tab, "Could not set the upstream", async token =>
        {
            await writer.SetUpstreamAsync(tab.Location, name, $"{GitRefPrefixes.Remotes}{upstream}", token).ConfigureAwait(false);
            return $"{name} now tracks {upstream}";
        });
        return string.Empty;
    }

    private void RefreshCandidates()
    {
        var refs = host.Workspace.FindTab(_tabId)?.Refs ?? [];
        _candidates = _kind == BranchPromptKind.Upstream
            ? SwitchCandidates.BuildRemote(refs, _text)
            : SwitchCandidates.Build(refs, _commitHash, _text);
        _index = 0;
    }

    private string SwitchDetail() => CandidateDetail("No matching branch");

    private string CandidateDetail(string none)
    {
        if (_index >= _candidates.Count)
        {
            return none;
        }

        var position = (_index + 1).ToString(CultureInfo.InvariantCulture);
        var total = _candidates.Count.ToString(CultureInfo.InvariantCulture);
        return $"{_candidates[_index].Label} ({position}/{total})";
    }

    // Finds the local branch a rename or a delete acts on, or returns why there is none.
    private string? ResolveLocalBranch(RepositoryState tab, out GitRef? branch)
    {
        branch = null;
        var chosen = selection();
        var locals = tab.Refs.Where(reference => reference.Kind == RefKind.LocalBranch).ToList();
        if (chosen.RowKind is not null)
        {
            branch = chosen.RowKind == SidebarRowKind.LocalBranch
                ? locals.FirstOrDefault(reference => reference.Name == chosen.Name)
                : null;
            return branch is null ? "Select a local branch first" : null;
        }

        var here = chosen.CommitHash is null
            ? []
            : locals.Where(reference => reference.CommitHash == chosen.CommitHash).ToList();
        if (here.Count > 1)
        {
            return $"{here.Count} branches point here: pick one in the sidebar";
        }

        branch = here.FirstOrDefault();
        return branch is null ? SelectNotice : null;
    }

    private string Accept(BranchPromptKind kind, TabId tabId)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return string.Empty;
        }

        // An operation may have started while the prompt was open (the refresh updates it).
        var checksOut = kind == BranchPromptKind.Switch || kind == BranchPromptKind.Create && _checkout;
        if (checksOut && OperationRefusal(tab) is { } refusal)
        {
            return refusal;
        }

        var text = _text.Trim();
        return kind switch
        {
            BranchPromptKind.Create => AcceptCreate(tab, text),
            BranchPromptKind.Rename => AcceptRename(tab, text),
            BranchPromptKind.Upstream => AcceptUpstream(tab),
            _ => AcceptSwitch(tab),
        };
    }

    private string AcceptCreate(RepositoryState tab, string name)
    {
        if (name.Length == 0)
        {
            return "No name: nothing was created";
        }

        if (HasLocalBranch(tab, name))
        {
            return $"Branch {name} already exists";
        }

        var start = _startPoint;
        var checkout = _checkout;
        var done = $"Created branch {name} at {_label}{(checkout ? " and checked it out" : string.Empty)}";
        Start(tab, "Could not create the branch", async token =>
        {
            await EnsureValidAsync(tab, name, token).ConfigureAwait(false);
            await writer.CreateAsync(tab.Location, name, start, checkout, token).ConfigureAwait(false);
            return done;
        }, CommitOf(tab, start));
        return string.Empty;
    }

    private string AcceptRename(RepositoryState tab, string name)
    {
        var from = _label;
        if (name.Length == 0 || name == from)
        {
            return "The name is unchanged: nothing was renamed";
        }

        if (HasLocalBranch(tab, name))
        {
            return $"Branch {name} already exists";
        }

        Start(tab, "Could not rename the branch", async token =>
        {
            await EnsureValidAsync(tab, name, token).ConfigureAwait(false);
            await writer.RenameAsync(tab.Location, from, name, token).ConfigureAwait(false);
            return $"Renamed {from} to {name}";
        }, CommitOf(tab, $"{GitRefPrefixes.Heads}{from}"));
        return string.Empty;
    }

    private string AcceptSwitch(RepositoryState tab)
    {
        if (_index >= _candidates.Count)
        {
            return "No matching branch";
        }

        var candidate = _candidates[_index];
        if (candidate.IsCurrent)
        {
            return $"Already on {candidate.Label}";
        }

        if (candidate.CheckedOutIn is { } path)
        {
            return $"{candidate.Label} is checked out in {path}: use that worktree (<space>ws)";
        }

        var target = candidate.Target;
        switch (candidate.Kind)
        {
            case SwitchTargetKind.LocalBranch:
                Start(tab, "Could not switch", async token =>
                {
                    await writer.SwitchAsync(tab.Location, target, token).ConfigureAwait(false);
                    return $"Switched to {target}";
                }, CommitOf(tab, $"{GitRefPrefixes.Heads}{target}"));
                break;
            case SwitchTargetKind.RemoteBranch:
                Start(tab, "Could not switch", async token =>
                {
                    await writer.SwitchToRemoteAsync(tab.Location, target, token).ConfigureAwait(false);
                    return $"Switched to new branch {SwitchCandidates.LocalNameOf(target)} tracking {target}";
                }, CommitOf(tab, $"{GitRefPrefixes.Remotes}{target}"));
                break;
            default:
                Start(tab, "Could not check out", async token =>
                {
                    await writer.SwitchDetachedAsync(tab.Location, target, token).ConfigureAwait(false);
                    return $"HEAD is detached at {ShortHash(target)}";
                }, CommitOf(tab, target));
                break;
        }

        return string.Empty;
    }

    private async Task EnsureValidAsync(RepositoryState tab, string name, CancellationToken token)
    {
        if (!await writer.IsValidNameAsync(tab.Location, name, token).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"'{name}' is not a valid branch name");
        }
    }

    private static bool HasLocalBranch(RepositoryState tab, string name) =>
        tab.Refs.Any(reference => reference.Kind == RefKind.LocalBranch && reference.Name == name);

    private string AskDelete(RepositoryState tab)
    {
        if (ResolveLocalBranch(tab, out var branch) is { } refusal)
        {
            return refusal;
        }

        if (branch!.IsCurrent)
        {
            return $"{branch.Name} is checked out: switch to another branch first";
        }

        if (branch.CheckedOutIn is { } path)
        {
            return $"{branch.Name} is checked out in {path}: use that worktree (<space>ws)";
        }

        var prompt = new ConfirmPrompt(
            "Delete branch",
            [$"Delete the branch {branch.Name} (at {ShortHash(branch.CommitHash)})?"],
            $"Undo: git branch {branch.Name} {branch.CommitHash}",
            "Delete branch");
        return confirm(prompt, () => StartDelete(tab.Id, branch)) ? string.Empty : TooSmallNotice;
    }

    private void StartDelete(TabId tabId, GitRef branch)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || !_busy.Add(tabId))
        {
            statuses.Warning($"{BusyNotice}; nothing was deleted");
            return;
        }

        var token = cancellationOf(tabId);
        _ = Task.Run(() => DeleteAsync(tab, branch, token), CancellationToken.None);
    }

    private async Task DeleteAsync(RepositoryState tab, GitRef branch, CancellationToken token)
    {
        try
        {
            var merged = await writer.IsMergedAsync(tab.Location, branch.Name, branch.UpstreamFullName, token)
                .ConfigureAwait(false);
            if (!merged)
            {
                postToUi(() => AskForce(tab, branch));
                return;
            }

            await writer.DeleteAsync(tab.Location, branch.Name, false, token).ConfigureAwait(false);
            postToUi(() => Finish(tab.Id, DeletedText(branch), StatusSeverity.Info, HeadCommit(tab.Id)));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _busy.Remove(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not delete: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error));
        }
    }

    // The branch holds commits that neither HEAD nor its upstream reach: a second, explicit answer is needed.
    private void AskForce(RepositoryState tab, GitRef branch)
    {
        _busy.Remove(tab.Id);
        var prompt = new ConfirmPrompt(
            "Delete unmerged branch",
            [
                $"{branch.Name} has commits that are not on HEAD or its upstream.",
                "Deleting it makes them unreachable.",
            ],
            $"Undo: git branch {branch.Name} {branch.CommitHash}",
            "Delete anyway");
        if (!confirm(prompt, () => StartForceDelete(tab, branch)))
        {
            statuses.Warning($"{branch.Name} is unmerged and the screen is too small to ask: nothing was deleted");
        }
    }

    private void StartForceDelete(RepositoryState tab, GitRef branch)
    {
        if (!_busy.Add(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was deleted");
            return;
        }

        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => ForceDeleteAsync(tab, branch, token), CancellationToken.None);
    }

    private async Task ForceDeleteAsync(RepositoryState tab, GitRef branch, CancellationToken token)
    {
        try
        {
            await writer.DeleteAsync(tab.Location, branch.Name, true, token).ConfigureAwait(false);
            postToUi(() => Finish(tab.Id, DeletedText(branch), StatusSeverity.Info, HeadCommit(tab.Id)));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _busy.Remove(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not delete: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error));
        }
    }

    private static string DeletedText(GitRef branch) =>
        $"Deleted branch {branch.Name} (was {ShortHash(branch.CommitHash)}; restore: " +
        $"git branch {branch.Name} {branch.CommitHash})";

    private void Start(
        RepositoryState tab,
        string failure,
        Func<CancellationToken, Task<string>> work,
        ObjectId? follow = null)
    {
        _busy.Add(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab.Id, failure, work, follow, token), CancellationToken.None);
    }

    // The commit a ref or a hash names, for the graph cursor to land on; null when it is not known.
    private static ObjectId? CommitOf(RepositoryState tab, string refOrHash)
    {
        var reference = tab.Refs.FirstOrDefault(candidate => candidate.FullName == refOrHash);
        return ObjectId.TryParse(reference?.CommitHash ?? refOrHash, out var id) ? id : null;
    }

    // The commit HEAD is on now (the branch checked out, read from the tab's refs).
    private ObjectId? HeadCommit(TabId tabId) =>
        host.Workspace.FindTab(tabId) is { } tab
        && tab.Refs.FirstOrDefault(item => item is { Kind: RefKind.LocalBranch, IsCurrent: true }) is { } current
        && ObjectId.TryParse(current.CommitHash, out var id)
            ? id
            : null;

    private async Task RunAsync(
        TabId tabId,
        string failure,
        Func<CancellationToken, Task<string>> work,
        ObjectId? follow,
        CancellationToken token)
    {
        try
        {
            var done = await work(token).ConfigureAwait(false);
            postToUi(() => Finish(tabId, done, StatusSeverity.Info, follow));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _busy.Remove(tabId));
        }
        catch (Exception exception)
        {
            postToUi(() => Finish(tabId, $"{failure}: {OperationText.Reason(exception)}", StatusSeverity.Error));
        }
    }

    // After a success the graph cursor follows: the saved selection is the commit the refreshed graph then selects.
    private void Finish(TabId tabId, string text, StatusSeverity severity, ObjectId? follow = null)
    {
        _busy.Remove(tabId);
        statuses.Post(text, severity);
        if (follow is { } commit && severity == StatusSeverity.Info)
        {
            host.Dispatch(new TabCommand(tabId, new SelectCommitCommand(commit)));
        }

        refresh(tabId);
    }
}
