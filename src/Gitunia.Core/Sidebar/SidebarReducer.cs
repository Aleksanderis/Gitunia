using System.Collections.Immutable;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Sidebar;

/// <summary>Pure transitions of a tab's sidebar.</summary>
public static class SidebarReducer
{
    /// <summary>Applies a sidebar command to the tab.</summary>
    public static RepositoryState Reduce(RepositoryState tab, SidebarCommand command)
    {
        var rows = SidebarBuilder.Build(tab);
        var cursor = SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, rows.Length);
        return command switch
        {
            MoveSidebarCursorCommand move => ListMotion.TryMove(move.Action, move.Count, cursor, rows.Length, move.PageHeight, out var target)
                ? WithCursor(tab, target)
                : tab,
            SetSidebarCursorCommand set => WithCursor(tab, SidebarBuilder.ClampCursor(set.Index, rows.Length)),
            ExpandSidebarCommand => Expand(tab, rows, cursor),
            CollapseSidebarCommand => Collapse(tab, rows, cursor),
            ToggleSidebarSectionCommand => Toggle(tab, rows, cursor),
            SetSidebarFilterCommand filter => SetFilter(tab, filter.Text),
            _ => tab,
        };
    }

    private static RepositoryState Expand(RepositoryState tab, ImmutableArray<SidebarRow> rows, int cursor)
    {
        if (rows.IsEmpty)
        {
            return tab;
        }

        var row = rows[cursor];
        if (!row.IsSection)
        {
            return tab with { View = tab.View with { FocusedPane = PaneKind.Graph } };
        }

        if (!row.IsExpanded)
        {
            return SetFolded(tab, row.Section, folded: false);
        }

        return cursor + 1 < rows.Length && !rows[cursor + 1].IsSection ? WithCursor(tab, cursor + 1) : tab;
    }

    private static RepositoryState Collapse(RepositoryState tab, ImmutableArray<SidebarRow> rows, int cursor)
    {
        if (rows.IsEmpty)
        {
            return tab;
        }

        var row = rows[cursor];
        if (row.IsSection)
        {
            return tab.Sidebar.HasFilter ? tab : SetFolded(tab, row.Section, folded: true);
        }

        for (var index = cursor - 1; index >= 0; index--)
        {
            if (rows[index].IsSection)
            {
                return WithCursor(tab, index);
            }
        }

        return tab;
    }

    private static RepositoryState Toggle(RepositoryState tab, ImmutableArray<SidebarRow> rows, int cursor)
    {
        if (rows.IsEmpty || !rows[cursor].IsSection || tab.Sidebar.HasFilter)
        {
            return tab;
        }

        var row = rows[cursor];
        return SetFolded(tab, row.Section, folded: row.IsExpanded);
    }

    private static RepositoryState SetFolded(RepositoryState tab, SidebarSection section, bool folded)
    {
        var collapsed = tab.Sidebar.CollapsedSections.Remove(section);
        if (folded)
        {
            collapsed = collapsed.Add(section);
        }

        return tab with { Sidebar = tab.Sidebar with { CollapsedSections = collapsed } };
    }

    private static RepositoryState SetFilter(RepositoryState tab, string text)
    {
        var filtered = tab with { Sidebar = tab.Sidebar with { FilterText = text, Cursor = 0 } };
        var rows = SidebarBuilder.Build(filtered);
        var firstEntry = rows.Length > 1 && text.Length > 0 ? 1 : 0;
        return WithCursor(filtered, firstEntry);
    }

    private static RepositoryState WithCursor(RepositoryState tab, int cursor) => tab with { Sidebar = tab.Sidebar with { Cursor = cursor } };
}
