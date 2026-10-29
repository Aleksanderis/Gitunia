using System.Collections;
using System.Collections.Immutable;
using System.Collections.Specialized;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Worktrees;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>
/// List source over the sidebar rows of the active tab. <see cref="Update"/> rebuilds the rows only when the data
/// or the folding/filter changed, not when just the cursor moved.
/// </summary>
public sealed class SidebarListSource(GraphGlyphSet glyphs) : IListDataSource
{
    private ImmutableArray<GitRef> _lastRefs;
    private ImmutableArray<WorktreeInfo> _lastWorktrees;
    private ImmutableArray<StashEntry> _lastStashes;
    private ImmutableArray<SidebarSection> _lastCollapsed;
    private string? _lastFilter;
    private ImmutableArray<string> _lastOpened;
    private ImmutableArray<string> _lastClosed;
    private string? _lastPath;
    private ImmutableDictionary<string, WorktreeStatus>? _lastOverview;
    private StatusSnapshot? _lastOwnStatus;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>The visible rows.</summary>
    public ImmutableArray<SidebarRow> Rows { get; private set; } = [];

    public int Count => Rows.Length;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    /// <summary>Rebuilds the rows when something they depend on changed. Returns true when the rows are new.</summary>
    /// <param name="tab">The active tab, or null when there is none.</param>
    public bool Update(RepositoryState? tab)
    {
        if (tab is null)
        {
            return _lastPath is not null && Set(null, [], default, default, default, default, null, null);
        }

        var state = tab.Sidebar;
        var unchanged = _lastPath == tab.CommandPath
            && _lastRefs == tab.Refs
            && _lastWorktrees == tab.Worktrees
            && _lastStashes == tab.Stashes
            && _lastCollapsed == state.CollapsedSections
            && _lastFilter == state.FilterText
            && _lastOpened == state.OpenedFolders
            && _lastClosed == state.ClosedFolders
            && ReferenceEquals(_lastOverview, tab.WorktreeStatuses)
            && ReferenceEquals(_lastOwnStatus, tab.WorkingTree?.Snapshot);
        if (unchanged)
        {
            return false;
        }

        // The worktree rows show counts from the overview and from the tab's own status, so those rebuild the rows too.
        _lastOverview = tab.WorktreeStatuses;
        _lastOwnStatus = tab.WorkingTree?.Snapshot;
        return Set(
            tab.CommandPath,
            SidebarBuilder.Build(tab),
            tab.Refs,
            tab.Worktrees,
            tab.Stashes,
            state.CollapsedSections,
            state.FilterText,
            state);
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        var paneWidth = width;
        var reserved = RowMenuMarker.ReservedColumns(paneWidth, PanePadding.Columns);
        width = PanePadding.TextWidth(width) - reserved;
        var line = new LineBuilder(width, glyphs.Ellipsis).Build();
        if (item >= 0 && item < Rows.Length)
        {
            line = SidebarRowRenderer.Render(Rows[item], width, selected, glyphs);
        }

        LinePainter.Paint(
            listView,
            RowMenuMarker.Apply(
                PanePadding.Wrap(RowMenuMarker.WithReserve(line, reserved)),
                paneWidth,
                item == listView.SelectedItem),
            col,
            row,
            listView.HasFocus);
    }

    public void Dispose()
    {
    }

    private bool Set(
        string? path,
        ImmutableArray<SidebarRow> rows,
        ImmutableArray<GitRef> refs,
        ImmutableArray<WorktreeInfo> worktrees,
        ImmutableArray<StashEntry> stashes,
        ImmutableArray<SidebarSection> collapsed,
        string? filter,
        SidebarState? state)
    {
        _lastPath = path;
        _lastRefs = refs;
        _lastWorktrees = worktrees;
        _lastStashes = stashes;
        _lastCollapsed = collapsed;
        _lastFilter = filter;
        _lastOpened = state?.OpenedFolders ?? default;
        _lastClosed = state?.ClosedFolders ?? default;
        Rows = rows;
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        return true;
    }
}
