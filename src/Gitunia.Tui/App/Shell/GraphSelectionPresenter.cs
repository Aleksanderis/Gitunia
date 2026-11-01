using Gitunia.Core.Workspaces.Repositories;
using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The graph cursor moved: the commit it is on becomes the tab's selection and the detail pane starts showing it. A
/// selection moved by the binder (reload, restore, tab switch) is not the user's choice (#716).
/// </summary>
public sealed class GraphSelectionPresenter
{
    private readonly Dispatcher _host;
    private readonly PaneViews _panes;
    private readonly GraphTabBinder _binder;
    private readonly StashViewController _stash;
    private readonly DetailPresenter _detail;

    /// <param name="host">Takes the selection commands.</param>
    /// <param name="panes">The graph list.</param>
    /// <param name="binder">Tells when the binder moves the selection.</param>
    /// <param name="stash">A shown stash stays while the binder moves the selection.</param>
    /// <param name="detail">Rebuilds the detail pane.</param>
    public GraphSelectionPresenter(
        Dispatcher host,
        PaneViews panes,
        GraphTabBinder binder,
        StashViewController stash,
        DetailPresenter detail)
    {
        _host = host;
        _panes = panes;
        _binder = binder;
        _stash = stash;
        _detail = detail;
        panes.Graph.ValueChanged += (_, _) => OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        var session = _detail.ActiveSession();
        var tabId = _host.Workspace.ActiveTabId;
        if (session is null || tabId is null || _panes.Graph.SelectedItem is not { } item)
        {
            return;
        }

        // While the binder swaps or restores the list, the tab's saved selection must not be overwritten, but the details still follow.
        var record = !_binder.IsSuspended;
        var keepStash = _stash.KeepsViewOnSelectionChange(_binder.IsSuspended || _binder.IsReplacing);
        var commitIndex = GraphListSource.CommitIndexOf(item);
        if (commitIndex >= 0 && commitIndex < session.Graph.Count)
        {
            var entry = session.Graph[commitIndex];
            if (record)
            {
                _host.Dispatch(new TabCommand(tabId.Value, new SelectCommitCommand(entry.Id)));
            }

            // Cheap on every move: the store shows "loading" at once and only asks git once the selection settles.
            if (!keepStash)
            {
                session.Details.Select(entry.Id, DetailPrefetchPlan.Around(session.Graph, commitIndex));
                _detail.RefreshDetail();
            }
        }
        else if (commitIndex >= session.Graph.Count && !keepStash)
        {
            // The continues-below marker holds no commit: the detail pane must not keep showing the previous one.
            _detail.RefreshDetail();
        }

        if (record)
        {
            _host.Dispatch(new TabCommand(tabId.Value, new ScrollToCommand(_panes.Graph.Viewport.Y)));
        }
    }
}
