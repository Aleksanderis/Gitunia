using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Pick;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Pick;

/// <summary>
/// Cherry-picks (<c>gp</c>) or reverts (<c>gv</c>) the selected commit, the commits of a Visual range in the graph, or
/// the tip of the selected sidebar branch or tag, onto the current branch (ux.md, workflow 3, "Integrating"). A
/// cherry-pick applies the range oldest first, a revert newest first (git's own order for reverting a range). When a
/// merge commit is among them, a one-key status bar prompt asks once which parent is the mainline. Nothing is lost, so
/// there is no Confirm; the status line names the old tip for the undo. A stop on conflicts is not a failure: the
/// in-progress operation takes over (banner, <c>gc</c>, <c>ga</c>, <c>gk</c>). Refused while another operation is in
/// progress and on an unborn HEAD; a detached HEAD is allowed, as git allows it. The git call runs off the UI thread,
/// one operation per tab.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs the cherry-pick or revert.</param>
/// <param name="reader">Reads the commits (subjects, and the parents of a merge commit) before anything runs.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="refresh">Reads a tab again (refs, graph, status); false when it cannot.</param>
/// <param name="setMode">Switches the keymap mode (Insert while the mainline prompt is open).</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="endRange">Ends the Visual range once the action took it.</param>
/// <param name="selection">What is selected now: a sidebar row, a graph commit or a Visual range.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with staging, discard, reset and merge.</param>
public sealed class PickActions(
    WorkspaceHost host,
    IPickWriter writer,
    IPickCommitReader reader,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<TabId, bool> refresh,
    Action<KeyMode> setMode,
    Action redraw,
    Action endRange,
    Func<PickSelection> selection,
    TabOperationGate gate) : IPromptHandler
{
    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) first";
    private const string DetachedLabel = "the detached HEAD";

    private TabId _tabId;
    private PickKind _kind;
    private IReadOnlyList<PickCommit> _commits = [];
    private int _mainlineCount;
    private bool _isOpen;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => !_isOpen || host.Workspace.ActiveTabId != _tabId
        ? string.Empty
        : PickTexts.MainlinePrompt(_kind, _commits.First(commit => commit.IsMerge));

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

        var kind = KindOf(action);
        if (kind is null)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is { } tab)
        {
            Notice = Begin(tab, kind.Value);
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

        if (key.Normalize() is { Modifiers: KeyModifiers.None } normalized && MainlineOf(normalized.Name) is { } parent)
        {
            if (parent < 1 || parent > _mainlineCount)
            {
                Notice = $"Choose a parent from 1 to {_mainlineCount} (Esc cancels)";
            }
            else
            {
                Close();
                Notice = Start(_tabId, _kind, _commits, parent);
            }
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }

    private static PickKind? KindOf(ActionId action)
    {
        if (action == ActionIds.PickCherry)
        {
            return PickKind.CherryPick;
        }

        return action == ActionIds.PickRevert ? PickKind.Revert : null;
    }

    private static string? Refusal(RepositoryState tab)
    {
        if (tab.Operation is { } operation)
        {
            var name = OperationBannerBuilder.NameOf(operation.Kind);
            return $"A {name} is in progress: finish or abort the {name} first (<space>gc continue, <space>ga abort)";
        }

        return tab.Head?.Kind == HeadKind.Unborn ? "The branch has no commits yet: nothing to pick onto" : null;
    }

    private static string TargetOf(RepositoryState tab) =>
        tab.Head is { Kind: HeadKind.Branch, BranchName: { } branch } ? branch : DetachedLabel;

    private static bool IsPickable(PickSelection chosen) =>
        chosen.Hashes.Count > 0
        && chosen.RowKind is null or SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch or SidebarRowKind.Tag;

    // Cherry-picks apply oldest first; the selection arrives newest first, as the graph shows it.
    private static List<string> InApplyOrder(PickKind kind, IReadOnlyList<string> hashes)
    {
        var ordered = hashes.ToList();
        if (kind == PickKind.CherryPick)
        {
            ordered.Reverse();
        }

        return ordered;
    }

    private static int? MainlineOf(string name) =>
        name.Length == 1 && char.IsAsciiDigit(name[0]) ? name[0] - '0' : null;

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

    private string Begin(RepositoryState tab, PickKind kind)
    {
        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        if (Refusal(tab) is { } refusal)
        {
            return refusal;
        }

        var chosen = selection();
        if (!IsPickable(chosen))
        {
            return SelectNotice;
        }

        var hashes = InApplyOrder(kind, chosen.Hashes);
        endRange();
        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => ReadAsync(tab, kind, hashes, token), CancellationToken.None);
        return string.Empty;
    }

    private async Task ReadAsync(RepositoryState tab, PickKind kind, List<string> hashes, CancellationToken token)
    {
        try
        {
            var commits = await reader.ReadAsync(tab.Location, hashes, token).ConfigureAwait(false);
            postToUi(() => Decide(tab.Id, kind, commits));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Finish(tab.Id, string.Empty, StatusSeverity.Info, false));
        }
        catch (Exception exception)
        {
            var failure = $"Could not read the commits, nothing was done: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, false));
        }
    }

    // A merge commit among the commits needs a mainline first; everything else runs at once.
    private void Decide(TabId tabId, PickKind kind, IReadOnlyList<PickCommit> commits)
    {
        gate.End(tabId);
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        // The state may have changed while the commits were read.
        if (Refusal(tab) is { } refusal)
        {
            statuses.Warning(refusal);
        }
        else if (!commits.Any(commit => commit.IsMerge))
        {
            if (Start(tab.Id, kind, commits, null) is { Length: > 0 } busy)
            {
                statuses.Warning(busy);
            }
        }
        else if (host.Workspace.ActiveTabId == tabId)
        {
            _tabId = tabId;
            _kind = kind;
            _commits = commits;
            _mainlineCount = commits.Where(commit => commit.IsMerge).Min(commit => commit.Parents.Count);
            _isOpen = true;
            setMode(KeyMode.Insert);
        }
        else
        {
            statuses.Warning("The tab changed: nothing was done");
        }

        redraw();
    }

    private string Start(TabId tabId, PickKind kind, IReadOnlyList<PickCommit> commits, int? mainline)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return string.Empty;
        }

        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        var target = TargetOf(tab);
        _ = Task.Run(() => PickAsync(tab, kind, commits, mainline, target, token), CancellationToken.None);
        return string.Empty;
    }

    private async Task PickAsync(
        RepositoryState tab,
        PickKind kind,
        IReadOnlyList<PickCommit> commits,
        int? mainline,
        string target,
        CancellationToken token)
    {
        var what = PickTexts.Describe(commits);
        try
        {
            var hashes = commits.Select(commit => commit.Hash).ToList();
            var result = await writer.PickAsync(tab.Location, kind, hashes, mainline, token).ConfigureAwait(false);
            var report = PickTexts.Report(result, kind, target, what);
            postToUi(() => Finish(tab.Id, report.Text, report.Severity, true));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Finish(tab.Id, string.Empty, StatusSeverity.Info, false));
        }
        catch (Exception exception)
        {
            var failure = PickTexts.Failure(kind, what, target, OperationText.Reason(exception));
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, true));
        }
    }

    private void Finish(TabId tabId, string text, StatusSeverity severity, bool refreshTab)
    {
        gate.End(tabId);
        statuses.Post(text, severity);
        if (refreshTab && host.Workspace.FindTab(tabId) is not null)
        {
            refresh(tabId);
        }
    }
}
