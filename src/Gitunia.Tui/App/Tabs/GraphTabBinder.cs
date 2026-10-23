using System.Collections.Immutable;
using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Tabs;

/// <summary>
/// Keeps the graph list showing the active tab: swaps the list source when the active tab changes and restores the tab's
/// own selection and scroll position, waiting for the streamed rows when the selected commit is not loaded yet.
/// </summary>
public sealed class GraphTabBinder(WorkspaceHost host, EffectRunner effects, ListView graph, GraphListSource emptySource, Action redraw)
{
    private TabSession? _goneSession;
    private ImmutableArray<GitRef> _goneRefs;
    private bool _hasShown;
    private TabId? _shownTabId;
    private TabSession? _shownSession;
    private bool _restorePending;
    private int _itemBeforeReplace;
    private int _rowBeforeReplace;

    /// <summary>Raised when the shown tab changed, with the session that was shown and the one that is shown now (either may be null).</summary>
    public event Action<TabSession?, TabSession?>? Switched;

    /// <summary>Raised after rows were appended or the shown tab changed, so searches can look at the new rows.</summary>
    public event Action? GraphChanged;

    /// <summary>True while the list is changed on purpose; selection changes then must not be written back to the workspace.</summary>
    public bool IsSuspended { get; private set; }

    /// <summary>Call after every workspace change: does nothing unless the active tab changed.</summary>
    public void Sync()
    {
        var activeId = host.Workspace.ActiveTabId;
        var session = activeId is { } shownId ? effects.SessionOf(shownId) : null;
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

        _shownSession = activeId is { } id ? effects.SessionOf(id) : null;
        IsSuspended = true;
        try
        {
            graph.Source = _shownSession?.Source ?? emptySource;
            graph.SelectedItem = null;
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
        redraw();
    }

    // The branches whose upstream is gone come from the tab's refs; rebuilt only when the refs were replaced.
    private void ShowUpstreamGone(TabSession? session)
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
        graph.SetNeedsDraw();
    }

    // The mark is part of the tab's state; the list source only draws it.
    private void ShowCompareMark(TabSession? session)
    {
        if (session is null
            || host.Workspace.ActiveTab is not { } tab
            || session.Source.MarkedCommit == tab.View.CompareMark)
        {
            return;
        }

        session.Source.MarkedCommit = tab.View.CompareMark;
        graph.SetNeedsDraw();
    }

    private void OnGraphReplacing()
    {
        _itemBeforeReplace = graph.SelectedItem ?? 0;
        _rowBeforeReplace = Math.Max(0, _itemBeforeReplace - graph.Viewport.Y);
    }

    // A refreshed log keeps the selected commit selected and on the same screen row; when the commit is gone the old position stays.
    private void OnGraphReplaced()
    {
        if (_shownSession is not { } session)
        {
            return;
        }

        var count = session.Source.Count;
        var wanted = host.Workspace.ActiveTab?.View.SelectedCommit;
        var index = wanted is { } commit ? session.Graph.IndexOf(commit) : -1;
        var item = index >= 0 ? index + ScreenLayout.ReservedLeadingRows : Math.Min(_itemBeforeReplace, Math.Max(0, count - 1));
        var top = Math.Clamp(item - _rowBeforeReplace, 0, Math.Max(0, count - graph.Viewport.Height));
        IsSuspended = index >= 0 || wanted is null;
        try
        {
            graph.SelectedItem = item;
            graph.Viewport = graph.Viewport with { Y = top };
        }
        finally
        {
            IsSuspended = false;
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

        graph.SetNeedsDraw();
        GraphChanged?.Invoke();
        redraw();
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

        var index = tab.View.SelectedCommit is { } wanted ? session.Graph.IndexOf(wanted) : -1;
        if (index < 0 && tab.View.SelectedCommit is not null && !session.IsGraphComplete && !session.ExpansionFailed)
        {
            // The remembered commit may lie below the soft row limit: load the rest and wait for it.
            session.RequestExpand();

            return;
        }

        var item = index >= 0 ? index + ScreenLayout.ReservedLeadingRows : 0;
        graph.SelectedItem = item;
        graph.EnsureSelectedItemVisible();
        var offset = tab.View.ScrollOffset;
        var height = graph.Viewport.Height;
        if (index >= 0 && offset <= item && item < offset + height && offset != graph.Viewport.Y)
        {
            graph.Viewport = graph.Viewport with { Y = Math.Min(offset, Math.Max(0, count - height)) };
        }

        _restorePending = false;
    }
}
