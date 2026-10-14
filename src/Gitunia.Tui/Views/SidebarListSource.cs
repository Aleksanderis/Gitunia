using System.Collections;
using System.Collections.Immutable;
using System.Collections.Specialized;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Worktrees;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views;

/// <summary>
/// List source over the sidebar rows of the active tab. <see cref="Update"/> rebuilds the rows only when the data
/// or the folding/filter changed, not when just the cursor moved.
/// </summary>
public sealed class SidebarListSource(GraphGlyphSet glyphs) : IListDataSource
{
    private ImmutableArray<GitRef> lastRefs;
    private ImmutableArray<WorktreeInfo> lastWorktrees;
    private ImmutableArray<StashEntry> lastStashes;
    private ImmutableArray<SidebarSection> lastCollapsed;
    private string? lastFilter;
    private string? lastPath;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>The visible rows.</summary>
    public ImmutableArray<SidebarRow> Rows { get; private set; } = [];

    public int Count => Rows.Length;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    /// <summary>Rebuilds the rows when something they depend on changed. Returns true when the rows are new.</summary>
    /// <param name="tab">The active tab, or null when there is none.</param>
    public bool Update(RepoState? tab)
    {
        if (tab is null)
        {
            return lastPath is not null && Set(null, [], default, default, default, default, null);
        }

        var state = tab.Sidebar;
        var unchanged = lastPath == tab.CommandPath
            && lastRefs == tab.Refs
            && lastWorktrees == tab.Worktrees
            && lastStashes == tab.Stashes
            && lastCollapsed == state.CollapsedSections
            && lastFilter == state.FilterText;
        return !unchanged && Set(tab.CommandPath, SidebarBuilder.Build(tab), tab.Refs, tab.Worktrees, tab.Stashes, state.CollapsedSections, state.FilterText);
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        width = PanePadding.TextWidth(width);
        var line = item >= 0 && item < Rows.Length
            ? SidebarRowRenderer.Render(Rows[item], width, selected, glyphs)
            : new LineBuilder(width, glyphs.Ellipsis).Build();
        LinePainter.Paint(listView, PanePadding.Wrap(line), col, row, listView.HasFocus);
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
        string? filter)
    {
        lastPath = path;
        lastRefs = refs;
        lastWorktrees = worktrees;
        lastStashes = stashes;
        lastCollapsed = collapsed;
        lastFilter = filter;
        Rows = rows;
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        return true;
    }
}
