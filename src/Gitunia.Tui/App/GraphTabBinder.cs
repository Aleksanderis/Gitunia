using Gitunia.Core.Workspaces;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// Keeps the graph list showing the active tab: swaps the list source when the active tab changes and restores the tab's
/// own selection and scroll position, waiting for the streamed rows when the selected commit is not loaded yet.
/// </summary>
public sealed class GraphTabBinder(WorkspaceHost host, EffectRunner effects, ListView graph, GraphListSource emptySource, Action redraw)
{
    private bool hasShown;
    private TabId? shownTabId;
    private TabSession? shownSession;
    private bool restorePending;
    private int itemBeforeReplace;
    private int rowBeforeReplace;

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
        if (hasShown && activeId == shownTabId)
        {
            return;
        }

        hasShown = true;
        shownTabId = activeId;
        var previousSession = shownSession;
        if (previousSession is not null)
        {
            previousSession.Changed -= OnSessionChanged;
            previousSession.GraphReplacing -= OnGraphReplacing;
            previousSession.GraphReplaced -= OnGraphReplaced;
        }

        shownSession = activeId is { } id ? effects.SessionOf(id) : null;
        IsSuspended = true;
        try
        {
            graph.Source = shownSession?.Source ?? emptySource;
            graph.SelectedItem = null;
            restorePending = shownSession is not null;
            Switched?.Invoke(previousSession, shownSession);
            if (shownSession is not null)
            {
                shownSession.Changed += OnSessionChanged;
                shownSession.GraphReplacing += OnGraphReplacing;
                shownSession.GraphReplaced += OnGraphReplaced;
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

    private void OnGraphReplacing()
    {
        itemBeforeReplace = graph.SelectedItem ?? 0;
        rowBeforeReplace = Math.Max(0, itemBeforeReplace - graph.Viewport.Y);
    }

    // A refreshed log keeps the selected commit selected and on the same screen row; when the commit is gone the old position stays.
    private void OnGraphReplaced()
    {
        if (shownSession is not { } session)
        {
            return;
        }

        var count = session.Source.Count;
        var wanted = host.Workspace.ActiveTab?.View.SelectedCommit;
        var index = wanted is { } commit ? session.Graph.IndexOf(commit) : -1;
        var item = index >= 0 ? index + ScreenLayout.ReservedLeadingRows : Math.Min(itemBeforeReplace, Math.Max(0, count - 1));
        var top = Math.Clamp(item - rowBeforeReplace, 0, Math.Max(0, count - graph.Viewport.Height));
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
    }

    private void OnSessionChanged()
    {
        if (restorePending)
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
        if (shownSession is not { } session || tab is null)
        {
            restorePending = false;
            return;
        }

        var count = session.Source.Count;
        if (count == 0)
        {
            restorePending = session.IsLoading;
            return;
        }

        var index = tab.View.SelectedCommit is { } wanted ? session.Graph.IndexOf(wanted) : -1;
        if (index < 0 && tab.View.SelectedCommit is not null && session.IsLoading)
        {
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

        restorePending = false;
    }
}
