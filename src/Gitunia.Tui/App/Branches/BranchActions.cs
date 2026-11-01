using Gitunia.App.Shell;
using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Branches;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Branches;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Branches;

/// <summary>
/// Create, switch, rename, delete branches and set or unset their upstream (ux-workflows.md, workflow 3, "Branches").
/// A name is typed in a prompt popup,
/// a switch checks out the branch under the cursor at once and asks in a type-to-filter prompt only when the cursor
/// names no single branch, and a delete goes through the Confirm dialog (a second one
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
/// <param name="busy">Marks the tab while an operation of this kind runs (one at a time; tab eviction waits for it).</param>
/// <param name="refresh">Reads a tab again (refs, graph, status); false when it cannot.</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
public sealed class BranchActions(
    Dispatcher host,
    IBranchWriter writer,
    StatusReporter statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    TabOperationGate busy,
    Func<TabId, bool> refresh,
    Action redraw,
    Func<BranchSelection> selection) : IPromptHandler
{
    private const string BusyNotice = "Busy: the last branch operation is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was deleted";
    private const string SelectNotice = "Select a local branch (sidebar) or a commit with one branch on it";

    private BranchPromptKind _kind;
    private TabId _tabId;
    private string _text = string.Empty;
    private bool _checkout;
    private string _startPoint = string.Empty;
    private string _label = string.Empty;
    private string? _commitHash;
    private readonly FilterPicker<SwitchCandidate> _picker = new();
    private int _deleteIndex;
    private IReadOnlySet<string>? _onlyBranches;
    private List<string> _deleteNames = [];

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup
    {
        get
        {
            if (_kind == BranchPromptKind.None || host.Workspace.ActiveTabId != _tabId)
            {
                return null;
            }

            return _kind switch
            {
                BranchPromptKind.Create => PromptPopup.ForText(
                    "New branch",
                    $"At {_label}{(_checkout ? string.Empty : " (not checked out)")}",
                    _text,
                    "create"),
                BranchPromptKind.Rename => PromptPopup.ForText("Rename branch", _label, _text, "rename"),
                BranchPromptKind.Delete => DeletePopup(),
                BranchPromptKind.Upstream => _picker.ToPopup(
                    "Set upstream",
                    $"Upstream of {_label}",
                    _text,
                    "set",
                    candidate => candidate.Label,
                    "No matching remote branch"),
                _ => _picker.ToPopup(
                    "Switch branch",
                    "Branch to check out",
                    _text,
                    "switch",
                    SwitchRow,
                    "No matching branch"),
            };
        }
    }

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
            // A remote branch (its row, or a graph commit that has only remote ones) is deleted by PushActions.
            if (RemoteDeleteTarget.IsRemoteOnly(tab, selection()))
            {
                return false;
            }

            Notice = BeginOperation(tab) ?? BeginDelete(tab);
        }
        else if (action == ActionIds.BranchSetUpstream)
        {
            Notice = BeginOperation(tab) ?? OpenUpstream(tab);
        }
        else if (action == ActionIds.BranchUnsetUpstream)
        {
            Notice = BeginOperation(tab) ?? UnsetUpstream(tab);
        }
        else if (action == ActionIds.BranchFastForward)
        {
            Notice = BeginOperation(tab) ?? FastForward(tab);
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
        if (_kind == BranchPromptKind.Delete)
        {
            // A list without a text field: j/k/h/l and the arrows move, so no letter is swallowed by a filter.
            if (PickerKeys.IsMove(normalized, out var move) || IsLetterMove(normalized, out move))
            {
                _deleteIndex = (_deleteIndex + move + _deleteNames.Count) % _deleteNames.Count;
            }
        }
        else if (_kind is BranchPromptKind.Switch or BranchPromptKind.Upstream && PickerKeys.IsMove(normalized, out var step))
        {
            _picker.Move(step);
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

        if ((BeginOperation(tab) ?? (checkout ? OperationRefusal(tab) : null)) is { } refusal)
        {
            return refusal;
        }

        _startPoint = commitHash;
        _label = ShortHash.Of(commitHash);
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

        if ((BeginOperation(tab) ?? OperationRefusal(tab)) is { } refusal)
        {
            return refusal;
        }

        Start(tab, "Could not check out", async token =>
        {
            await writer.SwitchDetachedAsync(tab.Location, commitHash, token).ConfigureAwait(false);
            return $"HEAD is detached at {ShortHash.Of(commitHash)}";
        }, CommitOf(tab, commitHash));
        return string.Empty;
    }

    private static bool IsLetterMove(KeyEvent key, out int step)
    {
        step = key.Modifiers == KeyModifiers.None ? key.Name switch
        {
            "j" or "l" => 1,
            "k" or "h" => -1,
            _ => 0,
        } : 0;
        return step != 0;
    }


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

    private string? BeginOperation(RepositoryState tab) => busy.IsBusy(tab.Id) ? BusyNotice : null;

    private void Close()
    {
        _kind = BranchPromptKind.None;
        host.SetKeyMode(KeyMode.Normal);
    }

    private void Open(BranchPromptKind kind, RepositoryState tab, string text)
    {
        _kind = kind;
        _tabId = tab.Id;
        _text = text;
        host.SetKeyMode(KeyMode.Insert);
    }

    private string OpenCreate(RepositoryState tab, bool checkout)
    {
        var chosen = selection();
        if (chosen.RowKind is SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch && chosen.Name is { } name)
        {
            // The full ref name, so a tag called like the branch cannot be picked instead.
            _startPoint = $"{GitRefPrefixes.Remotes}{name}";
            if (chosen.RowKind == SidebarRowKind.LocalBranch)
            {
                _startPoint = $"{GitRefPrefixes.Heads}{name}";
            }

            _label = name;
        }
        else if (chosen.CommitHash is { } hash)
        {
            _startPoint = hash;
            _label = ShortHash.Of(hash);
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

    // The branch under the cursor is checked out at once; a prompt opens only when the cursor names none, or
    // several (a graph commit with more than one branch label: the prompt lists just those).
    private string OpenSwitch(RepositoryState tab)
    {
        var chosen = selection();
        _onlyBranches = null;
        if (BranchUnderCursor(tab, chosen) is { } label
            && SwitchCandidates.Build(tab.Refs, null, string.Empty, new HashSet<string> { label }) is [var candidate])
        {
            return CheckoutCandidate(tab, candidate);
        }

        if (chosen.RowKind is null && chosen.CommitHash is { } hash)
        {
            var here = SwitchCandidates.BranchesAt(tab.Refs, hash);
            _onlyBranches = here.Count > 1 ? here : null;
        }

        _commitHash = chosen.RowKind is null ? chosen.CommitHash : null;
        Open(BranchPromptKind.Switch, tab, string.Empty);
        RefreshCandidates();
        return string.Empty;
    }

    // The label of the one branch the cursor names: a sidebar branch row, or the only branch label of the commit.
    private static string? BranchUnderCursor(RepositoryState tab, BranchSelection chosen)
    {
        if (chosen.RowKind is SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch && chosen.Name is { } name)
        {
            var hasTwin = tab.Refs.Any(reference =>
                reference.Kind == RefKind.LocalBranch && reference.Name == SwitchCandidates.LocalNameOf(name));
            return chosen.RowKind == SidebarRowKind.RemoteBranch && hasTwin ? SwitchCandidates.LocalNameOf(name) : name;
        }

        if (chosen.RowKind is null && chosen.CommitHash is { } hash)
        {
            var here = SwitchCandidates.BranchesAt(tab.Refs, hash);
            return here.Count == 1 ? here.First() : null;
        }

        return null;
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
        var shown = ShownUpstream(upstream);
        Start(tab, "Could not unset the upstream", async token =>
        {
            await writer.UnsetUpstreamAsync(tab.Location, name, token).ConfigureAwait(false);
            return $"{name} no longer tracks {shown} (restore: git branch --set-upstream-to={shown} {name})";
        });
        return string.Empty;
    }

    // Moves a local branch that is not checked out to its upstream as last fetched; git is the backstop for
    // the same refusals the refs already tell.
    private string FastForward(RepositoryState tab)
    {
        if (ResolveLocalBranch(tab, out var branch) is { } refusal)
        {
            return refusal;
        }

        var name = branch!.Name;
        if (branch.IsCurrent)
        {
            return $"{name} is checked out: pull it with <space>p instead";
        }

        if (branch.CheckedOutIn is { } path)
        {
            return $"{name} is checked out in {path}";
        }

        if (branch.UpstreamFullName is not { } upstream)
        {
            return $"{name} has no upstream";
        }

        var upstreamRef = tab.Refs.FirstOrDefault(reference => reference.FullName == upstream);
        if (branch.IsUpstreamGone || upstreamRef is null)
        {
            return $"The upstream of {name} is gone: nothing changed";
        }

        if (upstreamRef.CommitHash == branch.CommitHash)
        {
            return $"{name} is up to date with its upstream";
        }

        if (branch.Ahead is > 0)
        {
            return $"{name} has commits its upstream lacks (diverged): nothing changed";
        }

        var old = branch.CommitHash;
        Start(tab, "Could not fast-forward", async token =>
        {
            await writer.FastForwardAsync(tab.Location, name, upstream, token).ConfigureAwait(false);
            return $"Fast-forwarded {name} from {ShortHash.Of(old)} to {ShortHash.Of(upstreamRef.CommitHash)} "
                + $"(undo: git update-ref {GitRefPrefixes.Heads}{name} {old})";
        });
        return string.Empty;
    }

    private string AcceptUpstream(RepositoryState tab)
    {
        if (!_picker.TryGetSelected(out var candidate))
        {
            return "No matching remote branch";
        }

        var name = _label;
        var upstream = candidate.Target;
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
        if (_kind == BranchPromptKind.Upstream)
        {
            _picker.Reset(SwitchCandidates.BuildRemote(refs, _text));
            return;
        }

        _picker.Reset(SwitchCandidates.Build(refs, _commitHash, _text, _onlyBranches));
    }

    // A remote branch is checked out as a local branch of the same name without the remote prefix: say so up front.
    private static string SwitchRow(SwitchCandidate candidate)
    {
        if (candidate.Kind == SwitchTargetKind.RemoteBranch)
        {
            return $"{candidate.Label} -> local {SwitchCandidates.LocalNameOf(candidate.Target)}";
        }

        return candidate.Label;
    }

    // Finds the local branch a rename or a delete acts on, or returns why there is none.
    private string? ResolveLocalBranch(RepositoryState tab, out GitRef? branch)
    {
        branch = null;
        var chosen = selection();
        var locals = tab.Refs.Where(reference => reference.Kind == RefKind.LocalBranch).ToList();
        if (chosen.RowKind is not null)
        {
            if (chosen.RowKind == SidebarRowKind.LocalBranch)
            {
                branch = locals.FirstOrDefault(reference => reference.Name == chosen.Name);
            }

            return branch is null ? "Select a local branch first" : null;
        }

        var here = new List<GitRef>();
        if (chosen.CommitHash is not null)
        {
            here = [.. locals.Where(reference => reference.CommitHash == chosen.CommitHash)];
        }

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
            BranchPromptKind.Delete => AcceptDelete(tab),
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
        if (_picker.TryGetSelected(out var candidate))
        {
            return CheckoutCandidate(tab, candidate);
        }

        return "No matching branch";
    }

    private string CheckoutCandidate(RepositoryState tab, SwitchCandidate candidate)
    {
        if (candidate.IsCurrent)
        {
            return $"Already on {candidate.Label}";
        }

        if (candidate.CheckedOutIn is { } path)
        {
            return $"{candidate.Label} is checked out in {path}";
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
                    return $"HEAD is detached at {ShortHash.Of(target)}";
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

    // The upstream as shown to the user: without its refs/remotes/ or refs/heads/ prefix.
    private static string ShownUpstream(string upstream)
    {
        if (upstream.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal))
        {
            return upstream[GitRefPrefixes.Remotes.Length..];
        }

        if (upstream.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal))
        {
            return upstream[GitRefPrefixes.Heads.Length..];
        }

        return upstream;
    }

    private static bool HasLocalBranch(RepositoryState tab, string name) =>
        tab.Refs.Any(reference => reference.Kind == RefKind.LocalBranch && reference.Name == name);

    // The branch under the cursor goes to the Confirm dialog; a folder or a graph commit with several branches opens a
    // list to pick from first.
    private string BeginDelete(RepositoryState tab)
    {
        var chosen = selection();
        var names = DeleteChoices(tab, chosen);
        if (names.Count > 1)
        {
            _deleteNames = names;
            _deleteIndex = 0;
            _label = ShortHash.Of(chosen.CommitHash);
            if (chosen.RowKind == SidebarRowKind.Folder)
            {
                _label = $"{chosen.Name}/";
            }

            Open(BranchPromptKind.Delete, tab, string.Empty);
            return string.Empty;
        }

        if (chosen.RowKind == SidebarRowKind.Folder)
        {
            if (names.Count == 0)
            {
                return $"No local branch in {chosen.Name}/";
            }

            return AskDelete(tab, tab.Refs.First(reference => IsLocal(reference, names[0])));
        }

        return ResolveLocalBranch(tab, out var branch) is { } refusal ? refusal : AskDelete(tab, branch!);
    }

    private static bool IsLocal(GitRef reference, string name) =>
        reference.Kind == RefKind.LocalBranch && reference.Name == name;

    // The local branches a folder row holds, or those on the selected graph commit (the cursor is not a sidebar row).
    private static List<string> DeleteChoices(RepositoryState tab, BranchSelection chosen)
    {
        var locals = tab.Refs.Where(reference => reference.Kind == RefKind.LocalBranch);
        if (chosen.RowKind == SidebarRowKind.Folder)
        {
            var prefix = $"{chosen.Name}/";
            return [.. locals.Where(reference => reference.Name.StartsWith(prefix, StringComparison.Ordinal))
                .Select(reference => reference.Name)];
        }

        if (chosen.RowKind is null && chosen.CommitHash is { } hash)
        {
            return [.. locals.Where(reference => reference.CommitHash == hash).Select(reference => reference.Name)];
        }

        return [];
    }

    private PromptPopup DeletePopup()
    {
        var rows = PickerRows.Window(_deleteNames, _deleteIndex);
        var position = PickerRows.Position(_deleteIndex, _deleteNames.Count);
        return new PromptPopup(
            "Delete branch",
            [$"Branch to delete at {_label} {position}"],
            null,
            rows,
            [new("Enter", "choose"), new("j/k/h/l", "select"), new("Esc", "cancel")]);
    }

    private string AcceptDelete(RepositoryState tab)
    {
        var name = _deleteIndex < _deleteNames.Count ? _deleteNames[_deleteIndex] : null;
        var branch = tab.Refs.FirstOrDefault(reference => name is not null && IsLocal(reference, name));
        return branch is null ? "That branch is gone: nothing was deleted" : AskDelete(tab, branch);
    }

    private string AskDelete(RepositoryState tab, GitRef branch)
    {
        if (branch.IsCurrent)
        {
            return $"{branch.Name} is checked out: switch to another branch first";
        }

        if (branch.CheckedOutIn is { } path)
        {
            return $"{branch.Name} is checked out in {path}";
        }

        var prompt = new ConfirmPrompt(
            "Delete branch",
            [$"Delete the branch {branch.Name} (at {ShortHash.Of(branch.CommitHash)})?"],
            $"Undo: git branch {branch.Name} {branch.CommitHash}",
            "Delete branch");
        return host.Confirm(prompt, () => StartDelete(tab.Id, branch)) ? string.Empty : TooSmallNotice;
    }

    private void StartDelete(TabId tabId, GitRef branch)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || !busy.Begin(tabId))
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
            postToUi(() => busy.End(tab.Id));
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
        busy.End(tab.Id);
        var prompt = new ConfirmPrompt(
            "Delete unmerged branch",
            [
                $"{branch.Name} has commits that are not on HEAD or its upstream.",
                "Deleting it makes them unreachable.",
            ],
            $"Undo: git branch {branch.Name} {branch.CommitHash}",
            "Delete anyway");
        if (!host.Confirm(prompt, () => StartForceDelete(tab, branch)))
        {
            statuses.Warning($"{branch.Name} is unmerged and the screen is too small to ask: nothing was deleted");
        }
    }

    private void StartForceDelete(RepositoryState tab, GitRef branch)
    {
        if (!busy.Begin(tab.Id))
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
            postToUi(() => busy.End(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not delete: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error));
        }
    }

    private static string DeletedText(GitRef branch) =>
        $"Deleted branch {branch.Name} (was {ShortHash.Of(branch.CommitHash)}; restore: " +
        $"git branch {branch.Name} {branch.CommitHash})";

    private void Start(
        RepositoryState tab,
        string failure,
        Func<CancellationToken, Task<string>> work,
        ObjectId? follow = null)
    {
        busy.Begin(tab.Id);
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
    private ObjectId? HeadCommit(TabId tabId)
    {
        if (host.Workspace.FindTab(tabId) is { } tab
            && tab.Refs.FirstOrDefault(item => item is { Kind: RefKind.LocalBranch, IsCurrent: true }) is { } current
            && ObjectId.TryParse(current.CommitHash, out var id))
        {
            return id;
        }

        return null;
    }

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
            postToUi(() => busy.End(tabId));
        }
        catch (Exception exception)
        {
            postToUi(() => Finish(tabId, $"{failure}: {OperationText.Reason(exception)}", StatusSeverity.Error));
        }
    }

    // After a success the graph cursor follows: the saved selection is the commit the refreshed graph then selects.
    private void Finish(TabId tabId, string text, StatusSeverity severity, ObjectId? follow = null)
    {
        busy.End(tabId);
        statuses.Post(text, severity);
        if (follow is { } commit && severity == StatusSeverity.Info)
        {
            host.Dispatch(new TabCommand(tabId, new SelectCommitCommand(commit)));
        }

        refresh(tabId);
    }
}
