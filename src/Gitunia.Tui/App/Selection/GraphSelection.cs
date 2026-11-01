using Gitunia.App.Tabs;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Tui.App.Compare;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Selection;

/// <summary>The commit selected in the graph of the shown tab, and moving the graph cursor to a commit.</summary>
/// <param name="sessions">The shown tab's runtime session.</param>
/// <param name="registry">The sessions of all open tabs.</param>
/// <param name="panes">The graph list.</param>
/// <param name="stash">Tells whether a stash is shown in place of the commit.</param>
public sealed class GraphSelection(
    IActiveSessions sessions,
    TabSessionRegistry<TerminalTabSession> registry,
    PaneViews panes,
    StashViewController stash) : IGraphSelectionSource, IGraphCommitSelector, ICompareSource
{
    /// <summary>The commit commit actions act on: the graph selection, none while a stash is shown.</summary>
    public string? SelectedCommit() => stash.ActionTarget(GraphCommitHash());

    /// <inheritdoc />
    ObjectId? ICompareSource.SelectedCommitId() =>
        SelectedCommit() is { } hash && ObjectId.TryParse(hash, out var id) ? id : null;

    /// <inheritdoc />
    TerminalTabSession? ICompareSource.ActiveSession() => sessions.ActiveSession();

    /// <summary>The hash of the commit under the graph cursor, null on a row without one.</summary>
    public string? GraphCommitHash()
    {
        var index = panes.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        var graph = sessions.ActiveSession()?.Graph;

        return graph is not null && index >= 0 && index < graph.Count ? graph[index].Id.ToString() : null;
    }

    /// <summary>The root commit has no parents, so no outgoing edge; its menu also offers the rebase of the whole history (#561).</summary>
    public bool IsSelectedCommitRoot()
    {
        var index = panes.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        var graph = sessions.ActiveSession()?.Graph;

        return graph is not null
            && index >= 0
            && index < graph.Count
            && graph[index].Row.Edges.All(edge => edge.Kind != GraphEdgeKind.Outgoing);
    }

    /// <summary>Moves the graph cursor to a list item and scrolls it into view.</summary>
    /// <param name="item">The list item.</param>
    public void SelectItem(int item)
    {
        if ((panes.Graph.Source?.Count ?? 0) > item)
        {
            panes.Graph.SelectedItem = item;
            panes.Graph.EnsureSelectedItemVisible();
        }
    }

    /// <summary>Moves the graph cursor to a graph index and scrolls it into view.</summary>
    /// <param name="index">The index in the graph.</param>
    public void SelectIndex(int index) => SelectItem(index + ScreenLayout.ReservedLeadingRows);

    /// <summary>Moves the graph cursor to a commit; the reason it cannot is returned (empty on success).</summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="hash">The commit.</param>
    public string SelectCommit(TabId tabId, string hash)
    {
        var session = registry.GetValueOrDefault(tabId);
        if (session?.Graph is not { } graph || !ObjectId.TryParse(hash, out var id))
        {
            return "The graph is not loaded";
        }

        var index = graph.IndexOf(id);
        if (index < 0)
        {
            var shortHash = ShortHash.Of(hash);
            if (session.IsLoading)
            {
                return $"{shortHash} is not loaded yet";
            }

            return $"{shortHash} is not in the graph (it may be on no branch)";
        }

        SelectIndex(index);

        return string.Empty;
    }
}
