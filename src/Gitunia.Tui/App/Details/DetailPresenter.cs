using Gitunia.Core.Workspaces.Panes;
using Gitunia.App.Tabs;
using Gitunia.Core.Graph;
using System.Collections.Immutable;
using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.Core.Details;
using Gitunia.Core.IssueTracker.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Details;

/// <summary>
/// Brings the detail pane in line with the shown commit (or stash) and the tab's view state: the rows, the cursor and
/// the diff that follows it. The state asks for a rebuild whenever the bottom tab, the path style or the file tree
/// changed, whoever changed them (key, click, restored session).
/// </summary>
public sealed class DetailPresenter : IStashViewHost, IIssueLinkView
{
    private readonly Dispatcher _host;
    private readonly IActiveSessions _sessions;
    private readonly PaneViews _panes;
    private readonly DiffPaneInput _diffPane;
    private readonly LiveConfig _config;
    private DetailTreeState? _builtTree;

    /// <param name="host">The state.</param>
    /// <param name="sessions">The shown tab's runtime session.</param>
    /// <param name="panes">The graph and detail lists.</param>
    /// <param name="diffPane">Follows the detail cursor.</param>
    /// <param name="config">The glyphs the file tree rows are drawn with.</param>
    public DetailPresenter(
        Dispatcher host,
        IActiveSessions sessions,
        PaneViews panes,
        DiffPaneInput diffPane,
        LiveConfig config)
    {
        _host = host;
        _sessions = sessions;
        _panes = panes;
        _diffPane = diffPane;
        _config = config;
    }

    /// <inheritdoc />
    public TerminalTabSession? ActiveSession() => _sessions.ActiveSession();

    /// <inheritdoc />
    public bool IsMessageFocused() =>
        _host.Workspace.ActiveTab?.View is { FocusedPane: PaneKind.Detail, BottomTab: BottomTab.Details };

    /// <inheritdoc />
    public ImmutableArray<IssueReference> LinksOfSelectedRow()
    {
        var rows = ActiveSession()?.DetailSource.Content.Rows;
        if (_panes.Detail.SelectedItem is { } row && rows is not null && row >= 0 && row < rows.Count)
        {
            return [.. rows[row].References ?? []];
        }

        return [];
    }

    /// <summary>Moves the cursor of the detail list to a row and scrolls it into view.</summary>
    /// <param name="row">The row.</param>
    public void MoveCursor(int row)
    {
        if (row >= 0 && row < (_panes.Detail.Source?.Count ?? 0))
        {
            _panes.Detail.SelectedItem = row;
            _panes.Detail.EnsureSelectedItemVisible();
        }
    }

    /// <inheritdoc />
    public void ReselectGraphCommit()
    {
        var session = ActiveSession();
        if (session is not null && _panes.Graph.SelectedItem is { } item)
        {
            var commitIndex = GraphListSource.CommitIndexOf(item);
            if (commitIndex >= 0 && commitIndex < session.Graph.Count)
            {
                session.Details.Select(
                    session.Graph[commitIndex].Id,
                    DetailPrefetchPlan.Around(session.Graph, commitIndex));
            }
        }

        RefreshDetail();
    }

    /// <inheritdoc />
    public void RefreshDetail()
    {
        var session = ActiveSession();
        if (session is null)
        {
            return;
        }

        var commitIndex = _panes.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        var stashName = StashViewController.ShownName(session);
        var entry = StashViewController.ShownEntry(session)
            ?? (commitIndex >= 0 && commitIndex < session.Graph.Count ? session.Graph[commitIndex] : null);
        var previous = session.DetailSource.Content;
        var view = _host.Workspace.ActiveTab?.View;
        _builtTree = view?.DetailTree;
        var tab = view?.BottomTab ?? BottomTab.Changes;
        var content = DetailContentBuilder.Build(
            entry,
            session.Details.Current,
            tab,
            stashName is null ? FileTreeViewOf(session, entry, tab) : null,
            view?.PathStyle ?? FilePathStyle.NameFirst,
            session.IssueLinks,
            view?.DetailTree.ClosedFolders ?? DetailTreeState.Empty.ClosedFolders,
            stashName);
        var previousCursor = _panes.Detail.SelectedItem;
        session.DetailSource.Update(content);

        var landing = content.Tab == BottomTab.Changes && content.Files.Count > 0 ? content.FirstFileRow : 0;
        _panes.Detail.SelectedItem = session.DetailCursor.Decide(new DetailCursorRequest(
            previous.ShortHash,
            content.ShortHash,
            previous.Tab,
            content.Tab,
            previous.Files.Count,
            landing,
            _panes.Detail.SelectedItem,
            content.Rows.Count));
        var remapped = DetailCursorRemap.Row(previous, content, previousCursor);
        if (remapped >= 0)
        {
            _panes.Detail.SelectedItem = remapped;
        }

        _panes.Detail.EnsureSelectedItemVisible();
        _panes.Detail.SetNeedsDraw();
        _panes.Chrome.RefreshChrome();
        _diffPane.FollowSelection();
        if (remapped >= 0)
        {
            // The cursor may keep its index while the row under it changed, so no cursor event tells the diff.
            _diffPane.OnDetailCursorMoved();
        }
    }

    /// <summary>
    /// The tab of the bottom pane is repository tab state: whoever changed it (key, click, restored session), the rows
    /// follow. Call after every state change.
    /// </summary>
    public void Follow()
    {
        if (ActiveSession() is { } session
            && _host.Workspace.ActiveTab is { } tab
            && (session.DetailSource.Content.Tab != tab.View.BottomTab
                || session.DetailSource.Content.PathStyle != tab.View.PathStyle
                || tab.View.DetailTree != _builtTree))
        {
            RefreshDetail();
        }
    }

    // The tree is only listed while the Files tab is shown, so scrolling the graph on another tab starts no ls-tree.
    private FileTreeView? FileTreeViewOf(TerminalTabSession session, GraphEntry? entry, BottomTab tab)
    {
        if (tab != BottomTab.Files || entry is null || session.Tree is not { } tree)
        {
            return null;
        }

        tree.Store.Select(entry.Id);

        var state = _host.Workspace.FindTab(session.TabId)?.View.DetailTree ?? DetailTreeState.Empty;

        return new FileTreeView(tree.Store.Current, tree.RowsFor(state), state.Filter, SidebarGlyphs.For(_config.Glyphs));
    }
}
