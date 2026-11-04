using System.Collections.Immutable;
using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Views;
using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Tabs;

/// <summary>
/// Keeps the graph list showing the active tab: swaps the list source when the active tab changes and restores the tab's
/// own selection and scroll position, waiting for the streamed rows when the selected commit is not loaded yet.
/// </summary>
public sealed class GraphTabBinder(
    Dispatcher host,
    TabSessionRegistry<TerminalTabSession> registry,
    IPaneLists lists,
    GraphListSource emptySource,
    IPaneChrome chrome)
{
    private readonly ListView _graph = lists.ListOf(PaneKind.Graph);
    private TerminalTabSession? _goneSession;
    private ImmutableArray<GitRef> _goneRefs;
    private bool _hasShown;
    private TabId? _shownTabId;
    private TerminalTabSession? _shownSession;
    private bool _restorePending;
    private int _itemBeforeReplace;
    private int _rowBeforeReplace;
    private bool _followsHeadBeforeReplace;

    // Tabs that were put on HEAD once; a reloaded session (worktree switch, evicted tab) keeps the tab's own selection.
    private readonly HashSet<TabId> _openedTabs = [];

    /// <summary>Raised when the shown tab changed, with the session that was shown and the one that is shown now (either may be null).</summary>
    public event Action<TerminalTabSession?, TerminalTabSession?>? Switched;

    /// <summary>Raised after rows were appended or the shown tab changed, so searches can look at the new rows.</summary>
    public event Action? GraphChanged;

    /// <summary>
    /// Raised after a reloaded graph was put back under the selection, also when the cursor row did not change (so the
    /// list raised no event) although another commit is on it now (#1206). Raised while <see cref="IsReplacing"/> is true.
    /// </summary>
    public event Action? SelectionReplaced;

    /// <summary>True while the list is changed on purpose; selection changes then must not be written back to the workspace.</summary>
    public bool IsSuspended { get; private set; }

    /// <summary>True while a reloaded graph is put back under the selection; the row moves without the user.</summary>
    public bool IsReplacing { get; private set; }

    /// <summary>Call after every workspace change: does nothing unless the active tab changed.</summary>
    public void Sync()
    {
        var activeId = host.Workspace.ActiveTabId;
        var session = activeId is { } shownId ? registry.GetValueOrDefault(shownId) : null;
        ShowUpstreamGone(session);
        ShowCompareMark(session);

        // A tab pointed at another worktree gets a new session under the same id: its graph is shown from here on.
        if (_hasShown && activeId == _shownTabId && session == _shownSession)
        {
            return;
        }

        _hasShown = true;
        _shownTabId = activeId;
        var previousSession = _shownSession;
        if (previousSession is not null)
        {
            previousSession.Changed -= OnSessionChanged;
            previousSession.GraphReplacing -= OnGraphReplacing;
            previousSession.GraphReplaced -= OnGraphReplaced;
        }

        _shownSession = activeId is { } id ? registry.GetValueOrDefault(id) : null;

        IsSuspended = true;
        try
        {
            _graph.Source = _shownSession?.Source ?? emptySource;
            _graph.SelectedItem = null;
            _restorePending = _shownSession is not null;
            Switched?.Invoke(previousSession, _shownSession);
            if (_shownSession is not null)
            {
                _shownSession.Changed += OnSessionChanged;
                _shownSession.GraphReplacing += OnGraphReplacing;
                _shownSession.GraphReplaced += OnGraphReplaced;
                TryRestore();
            }
        }
        finally
        {
            IsSuspended = false;
        }

        GraphChanged?.Invoke();
        chrome.RefreshChrome();
    }

    // The branches whose upstream is gone come from the tab's refs; rebuilt only when the refs were replaced.
    private void ShowUpstreamGone(TerminalTabSession? session)
    {
        if (session is null
            || host.Workspace.ActiveTab is not { } tab
            || (ReferenceEquals(session, _goneSession) && tab.Refs == _goneRefs))
        {
            return;
        }

        _goneSession = session;
        _goneRefs = tab.Refs;
        session.Source.UpstreamGoneBranches = UpstreamGoneBranches.Of(tab.Refs);
        _graph.SetNeedsDraw();
    }

    // The mark is part of the tab's state; the list source only draws it.
    private void ShowCompareMark(TerminalTabSession? session)
    {
        if (session is null
            || host.Workspace.ActiveTab is not { } tab
            || session.Source.MarkedCommit == tab.View.CompareMark)
        {
            return;
        }

        session.Source.MarkedCommit = tab.View.CompareMark;
        _graph.SetNeedsDraw();
    }

    private void OnGraphReplacing()
    {
        _itemBeforeReplace = _graph.SelectedItem ?? 0;
        _rowBeforeReplace = Math.Max(0, _itemBeforeReplace - _graph.Viewport.Y);
        _followsHeadBeforeReplace = IsOnHead(_itemBeforeReplace);
    }

    // A cursor on HEAD (on the newest commit when there is no HEAD yet) follows HEAD to its new place; a cursor in the history stays where it is.
    private bool IsOnHead(int item)
    {
        if (_shownSession is not { } session)
        {
            return false;
        }

        var commitIndex = GraphListSource.CommitIndexOf(item);

        if (commitIndex < 0 || commitIndex >= session.Graph.Count)
        {
            return false;
        }

        var headIndex = session.Graph.HeadIndex();

        return commitIndex == (headIndex >= 0 ? headIndex : 0);
    }

    // A refreshed log keeps the selected commit selected and on the same screen row; when the commit is gone the old position stays.
    private void OnGraphReplaced()
    {
        IsReplacing = true;
        try
        {
            RestoreAfterReplace();
            SelectionReplaced?.Invoke();
        }
        finally
        {
            IsReplacing = false;
        }
    }

    private void RestoreAfterReplace()
    {
        if (_shownSession is not { } session)
        {
            return;
        }

        var count = session.Source.Count;
        var wanted = host.Workspace.ActiveTab?.View.SelectedCommit;
        var headIndex = _followsHeadBeforeReplace ? session.Graph.HeadIndex() : -1;
        var index = headIndex >= 0 ? headIndex : IndexOfSelected(session, wanted);
        var item = index >= 0 ? index + ScreenLayout.ReservedLeadingRows : Math.Min(_itemBeforeReplace, Math.Max(0, count - 1));
        var top = Math.Clamp(item - _rowBeforeReplace, 0, Math.Max(0, count - _graph.Viewport.Height));
        if (headIndex >= 0 && item < _graph.Viewport.Height)
        {
            top = 0;
        }

        IsSuspended = index >= 0 || wanted is null;
        try
        {
            _graph.SelectedItem = item;
            _graph.Viewport = _graph.Viewport with { Y = top };
        }
        finally
        {
            IsSuspended = false;
        }

        // The cursor followed HEAD to another commit: the saved selection and scroll follow it.
        if (headIndex >= 0 && session.Graph[headIndex].Id != wanted)
        {
            SelectAndRemember(session.Graph[headIndex].Id, top);

            return;
        }

        // The selected commit is gone: the row now shows another one, so the saved selection must follow even when the list raised no event.
        if (index < 0 && wanted is not null && host.Workspace.ActiveTabId is { } tabId)
        {
            var commitIndex = item - ScreenLayout.ReservedLeadingRows;
            if (commitIndex >= 0 && commitIndex < session.Graph.Count)
            {
                host.Dispatch(new TabCommand(tabId, new SelectCommitCommand(session.Graph[commitIndex].Id)));
            }
        }
    }

    private static int IndexOfSelected(TerminalTabSession session, ObjectId? wanted) =>
        wanted is { } commit ? session.Graph.IndexOf(commit) : -1;

    private void SelectAndRemember(ObjectId commit, int top)
    {
        if (host.Workspace.ActiveTabId is not { } tabId)
        {
            return;
        }

        host.Dispatch(new TabCommand(tabId, new SelectCommitCommand(commit)));
        host.Dispatch(new TabCommand(tabId, new ScrollToCommand(top)));
    }

    private void OnSessionChanged()
    {
        if (_restorePending)
        {
            IsSuspended = true;
            try
            {
                TryRestore();
            }
            finally
            {
                IsSuspended = false;
            }
        }

        _graph.SetNeedsDraw();
        GraphChanged?.Invoke();
        chrome.RefreshChrome();
    }

    private void TryRestore()
    {
        var tab = host.Workspace.ActiveTab;
        if (_shownSession is not { } session || tab is null)
        {
            _restorePending = false;
            return;
        }

        var count = session.Source.Count;
        if (count == 0)
        {
            _restorePending = session.IsLoading;
            return;
        }

        if (!_openedTabs.Contains(tab.Id))
        {
            OpenAtHead(session, tab);

            return;
        }

        var index = tab.View.SelectedCommit is { } wanted ? session.Graph.IndexOf(wanted) : -1;
        if (index < 0 && tab.View.SelectedCommit is not null && !session.IsGraphComplete && !session.ExpansionFailed)
        {
            // The remembered commit may lie below the soft row limit: load the rest and wait for it.
            session.RequestExpand();

            return;
        }

        var item = index >= 0 ? index + ScreenLayout.ReservedLeadingRows : 0;
        _graph.SelectedItem = item;
        _graph.EnsureSelectedItemVisible();
        var offset = tab.View.ScrollOffset;
        var height = _graph.Viewport.Height;
        if (index >= 0 && offset <= item && item < offset + height && offset != _graph.Viewport.Y)
        {
            _graph.Viewport = _graph.Viewport with { Y = Math.Min(offset, Math.Max(0, count - height)) };
        }

        _restorePending = false;
    }

    // The first time a tab is shown it opens on HEAD, not on the selection saved by the last session.
    private void OpenAtHead(TerminalTabSession session, RepositoryState tab)
    {
        var headIndex = session.Graph.HeadIndex();
        if (headIndex < 0 && session.IsLoading)
        {
            // HEAD may still be streaming in: show the newest commit meanwhile and decide when it arrives or the load ends.
            _graph.SelectedItem ??= ScreenLayout.ReservedLeadingRows;

            return;
        }

        var index = Math.Max(0, headIndex);
        _graph.SelectedItem = index + ScreenLayout.ReservedLeadingRows;
        _graph.Viewport = _graph.Viewport with { Y = 0 };
        _graph.EnsureSelectedItemVisible();
        _openedTabs.Add(tab.Id);
        _restorePending = false;
        if (index < session.Graph.Count
            && (tab.View.SelectedCommit != session.Graph[index].Id || tab.View.ScrollOffset != _graph.Viewport.Y))
        {
            SelectAndRemember(session.Graph[index].Id, _graph.Viewport.Y);
        }
    }
}
