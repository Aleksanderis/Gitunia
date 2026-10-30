using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>The full status of one tab and the row selected in it. Pure data: refreshed by
/// <see cref="WorkingTreeLoadedCommand"/>.</summary>
/// <param name="Snapshot">The last status read.</param>
/// <param name="Selection">The selected row, null when the lists are empty.</param>
public sealed record WorkingTreeState(StatusSnapshot Snapshot, WorkingTreeSelection? Selection)
{
    private IReadOnlyList<StatusEntry>? _unstaged;
    private IReadOnlyList<StatusEntry>? _staged;
    private RowsCache? _unstagedRows;
    private RowsCache? _stagedRows;

    /// <summary>How the lists lay out their rows; the tree style adds folder rows. Follows the tab's path
    /// style.</summary>
    public FilePathStyle PathStyle { get; init; } = FilePathStyle.NameFirst;

    /// <summary>Folders of the Unstaged list that are closed in the tree style.</summary>
    public ImmutableHashSet<string> CollapsedUnstaged { get; init; } = [];

    /// <summary>Folders of the Staged list that are closed in the tree style.</summary>
    public ImmutableHashSet<string> CollapsedStaged { get; init; } = [];

    /// <summary>
    /// Entries of the Unstaged list in display order: conflicted paths first (their own group, ux-workflows.md workflow 2), then the worktree
    /// changes and untracked paths, each group in git's order.
    /// </summary>
    public IReadOnlyList<StatusEntry> Unstaged => _unstaged ??= Snapshot.Entries
        .Where(IsUnstagedRow)
        .OrderBy(entry => entry.Kind == StatusEntryKind.Unmerged ? 0 : 1)
        .ToList();

    /// <summary>Entries of the Staged list, in git's order.</summary>
    public IReadOnlyList<StatusEntry> Staged => _staged ??= Snapshot.Entries.Where(entry => entry.IsStaged).ToList();

    /// <summary>The entries of a list.</summary>
    public IReadOnlyList<StatusEntry> EntriesOf(WorkingTreeSection section) =>
        section == WorkingTreeSection.Unstaged ? Unstaged : Staged;

    /// <summary>The folders of a list that are closed.</summary>
    public ImmutableHashSet<string> CollapsedOf(WorkingTreeSection section) =>
        section == WorkingTreeSection.Unstaged ? CollapsedUnstaged : CollapsedStaged;

    /// <summary>
    /// The rows of a list as the path style lays them out. The selection's row index refers to these rows; in the flat
    /// styles a row is an entry.
    /// </summary>
    public WorkingRows RowsOf(WorkingTreeSection section)
    {
        var collapsed = CollapsedOf(section);
        var cache = section == WorkingTreeSection.Unstaged ? _unstagedRows : _stagedRows;
        if (cache is not null && cache.Style == PathStyle && ReferenceEquals(cache.Collapsed, collapsed))
        {
            return cache.Rows;
        }

        var paths = EntriesOf(section).Select(entry => entry.Path).ToList();
        var rows = PathStyle == FilePathStyle.Tree ? WorkingRows.Tree(paths, collapsed) : WorkingRows.Flat(paths);
        var built = new RowsCache(rows, PathStyle, collapsed);
        if (section == WorkingTreeSection.Unstaged)
        {
            _unstagedRows = built;
        }
        else
        {
            _stagedRows = built;
        }

        return rows;
    }

    /// <summary>The file the selection stands on, null when a folder or nothing is selected.</summary>
    public StatusEntry? SelectedEntry
    {
        get
        {
            if (Selection is not { IsFolder: false } selection)
            {
                return null;
            }

            return EntriesOf(selection.Section).FirstOrDefault(entry => entry.Path == selection.Path);
        }
    }

    /// <summary>The files under the selected folder, in list order; empty when a file or nothing is selected.</summary>
    public IReadOnlyList<StatusEntry> SelectedFolderEntries
    {
        get
        {
            if (Selection is not { IsFolder: true } selection)
            {
                return [];
            }

            var entries = EntriesOf(selection.Section);

            return [.. RowsOf(selection.Section).EntriesUnder(selection.Path).Select(index => entries[index])];
        }
    }

    /// <summary>The selection on a row of a list, null when the list has no such row.</summary>
    /// <param name="section">The list.</param>
    /// <param name="row">The row index.</param>
    public WorkingTreeSelection? SelectionAt(WorkingTreeSection section, int row)
    {
        var rows = RowsOf(section);
        if (row < 0 || row >= rows.Count)
        {
            return null;
        }

        var found = rows[row];

        if (found.IsFolder)
        {
            return new WorkingTreeSelection(section, found.FolderPath, row, true);
        }

        return new WorkingTreeSelection(section, EntriesOf(section)[found.EntryIndex].Path, row);
    }

    /// <summary>
    /// The state with another layout; the selection stays on the same file or folder (on the folder that
    /// hides the file,
    /// or on the first file under a folder that the flat styles do not show).
    /// </summary>
    /// <param name="style">The path style.</param>
    /// <param name="collapsedUnstaged">The closed folders of the Unstaged list.</param>
    /// <param name="collapsedStaged">The closed folders of the Staged list.</param>
    public WorkingTreeState WithLayout(
        FilePathStyle style,
        ImmutableHashSet<string> collapsedUnstaged,
        ImmutableHashSet<string> collapsedStaged)
    {
        var next = this with
        {
            PathStyle = style,
            CollapsedUnstaged = collapsedUnstaged,
            CollapsedStaged = collapsedStaged,
            Selection = null,
        };
        if (Selection is not { } selection)
        {
            return next;
        }

        var row = next.Locate(selection);

        return next with { Selection = next.SelectionAt(selection.Section, row) ?? next.SelectionNear(selection) };
    }

    /// <summary>Builds the state for a new snapshot, keeping the selection when its row is still listed.</summary>
    /// <remarks>
    /// Order of preference: the same file or folder in the same list; the row now at the old index (clamped),
    /// which is also what happens when
    /// the file moved to the other list by stage or unstage, so the cursor stays put and the action can be repeated (ux-workflows.md, workflow 2);
    /// the first row of the other list. Without a previous selection the first row is chosen.
    /// Never selects when both lists are empty.
    /// </remarks>
    /// <param name="previous">The state the snapshot replaces, null for the first one.</param>
    /// <param name="snapshot">The new status.</param>
    /// <param name="style">The path style of the tab; the previous state's when null.</param>
    public static WorkingTreeState Reconcile(
        WorkingTreeState? previous,
        StatusSnapshot snapshot,
        FilePathStyle? style = null)
    {
        var next = new WorkingTreeState(snapshot, null)
        {
            PathStyle = style ?? previous?.PathStyle ?? FilePathStyle.NameFirst,
            CollapsedUnstaged = previous?.CollapsedUnstaged ?? [],
            CollapsedStaged = previous?.CollapsedStaged ?? [],
        };
        var old = previous?.Selection;

        return next with { Selection = next.SelectionAfterSnapshot(old) };
    }

    /// <summary>Whether two snapshots hold the same content (the record's list members compare by reference).</summary>
    public static bool SameContent(StatusSnapshot first, StatusSnapshot second) =>
        first.Branch == second.Branch
        && first.Entries.SequenceEqual(second.Entries)
        && first.Warnings.SequenceEqual(second.Warnings);

    private static bool IsUnstagedRow(StatusEntry entry) => entry.Kind switch
    {
        StatusEntryKind.Ignored => false,
        StatusEntryKind.Untracked or StatusEntryKind.Unmerged => true,
        _ => entry.IsUnstaged,
    };

    private WorkingTreeSelection? SelectionAfterSnapshot(WorkingTreeSelection? old)
    {
        if (old is null)
        {
            return SelectionAt(WorkingTreeSection.Unstaged, 0) ?? SelectionAt(WorkingTreeSection.Staged, 0);
        }

        var other = WorkingTreeSection.Unstaged;
        if (old.Section == WorkingTreeSection.Unstaged)
        {
            other = WorkingTreeSection.Staged;
        }

        return SelectionAt(old.Section, Locate(old)) ?? SelectionNear(old) ?? SelectionAt(other, 0);
    }

    // The row now at the old index (clamped) of the same list, null when the list is empty.
    private WorkingTreeSelection? SelectionNear(WorkingTreeSelection old)
    {
        var count = RowsOf(old.Section).Count;

        return count == 0 ? null : SelectionAt(old.Section, Math.Min(old.Index, count - 1));
    }

    // The row a selection stands on in the current rows, -1 when its file or folder is gone.
    private int Locate(WorkingTreeSelection selection)
    {
        var rows = RowsOf(selection.Section);
        if (selection.IsFolder)
        {
            var folderRow = rows.RowOfFolder(selection.Path);
            if (folderRow >= 0)
            {
                return folderRow;
            }

            // The flat styles have no folder rows: the first file under the folder stands for it.
            var first = rows.EntriesUnder(selection.Path);

            return first.Count == 0 ? -1 : RowOfPath(selection.Section, rows, first[0]);
        }

        var index = IndexOf(EntriesOf(selection.Section), selection.Path);

        return index < 0 ? -1 : RowOfPath(selection.Section, rows, index);
    }

    private int RowOfPath(WorkingTreeSection section, WorkingRows rows, int entryIndex)
    {
        var row = rows.RowOfEntry(entryIndex);

        return row >= 0 ? row : rows.RowOfClosedAncestor(EntriesOf(section)[entryIndex].Path);
    }

    private static int IndexOf(IReadOnlyList<StatusEntry> list, string path)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Path == path)
            {
                return i;
            }
        }

        return -1;
    }

    private sealed record RowsCache(WorkingRows Rows, FilePathStyle Style, ImmutableHashSet<string> Collapsed);
}
