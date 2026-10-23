using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Worktrees;

namespace Gitunia.Core.Sidebar;

/// <summary>Builds the visible sidebar rows of a tab: pure, from the tab's refs, worktrees, stashes and sidebar state.</summary>
public static class SidebarBuilder
{
    private const string DetachedText = "detached";

    /// <summary>The rows in display order: each non-empty section header followed by its entries unless the section is folded.</summary>
    /// <remarks>An active filter keeps only matching entries, drops sections without matches and unfolds the rest.</remarks>
    public static ImmutableArray<SidebarRow> Build(RepositoryState tab)
    {
        var state = tab.Sidebar;
        var rows = ImmutableArray.CreateBuilder<SidebarRow>();
        foreach (var section in Enum.GetValues<SidebarSection>())
        {
            var entries = Entries(tab, section).Where(entry => Matches(entry, state.FilterText)).ToList();
            if (state.HasFilter && entries.Count == 0)
            {
                continue;
            }

            var isExpanded = state.HasFilter || !state.CollapsedSections.Contains(section);
            rows.Add(new SidebarRow(
                SidebarRowKind.Section,
                section,
                Title(section),
                string.Empty,
                null,
                false,
                isExpanded,
                entries.Count,
                null,
                null,
                Depth: -1));
            if (isExpanded)
            {
                var isGrouped = section is SidebarSection.Branches or SidebarSection.Remotes;
                rows.AddRange(isGrouped ? SidebarFolders.Group(entries, state) : entries);
            }
        }

        return rows.ToImmutable();
    }

    /// <summary>The cursor kept inside the rows (0 for an empty list).</summary>
    public static int ClampCursor(int cursor, int rowCount) => Math.Clamp(cursor, 0, Math.Max(0, rowCount - 1));

    /// <summary>The title of a section.</summary>
    public static string Title(SidebarSection section) => section switch
    {
        SidebarSection.Branches => "Branches",
        SidebarSection.Remotes => "Remotes",
        SidebarSection.Tags => "Tags",
        SidebarSection.Worktrees => "Worktrees",
        _ => "Stashes",
    };

    private static IEnumerable<SidebarRow> Entries(RepositoryState tab, SidebarSection section) => section switch
    {
        SidebarSection.Branches => References(tab, RefKind.LocalBranch, SidebarSection.Branches, SidebarRowKind.LocalBranch),
        SidebarSection.Remotes => References(tab, RefKind.RemoteBranch, SidebarSection.Remotes, SidebarRowKind.RemoteBranch),
        SidebarSection.Tags => References(tab, RefKind.Tag, SidebarSection.Tags, SidebarRowKind.Tag),
        SidebarSection.Worktrees => tab.Worktrees.Select(worktree => WorktreeRow(tab, worktree)),
        _ => tab.Stashes.Select(StashRow),
    };

    private static IEnumerable<SidebarRow> References(RepositoryState tab, RefKind kind, SidebarSection section, SidebarRowKind rowKind) =>
        tab.Refs
            // Symbolic refs such as origin/HEAD are aliases, not branches of their own.
            .Where(reference => reference.Kind == kind && reference.SymbolicTarget is null)
            .OrderBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.Name, StringComparer.Ordinal)
            .Select(reference => new SidebarRow(
                rowKind,
                section,
                reference.Name,
                reference.CheckedOutIn is { } path ? DirectoryName(path) : string.Empty,
                reference.CommitHash,
                reference.IsCurrent,
                false,
                0,
                reference.Ahead,
                reference.Behind,
                IsUpstreamGone: rowKind == SidebarRowKind.LocalBranch && reference.IsUpstreamGone));

    private static SidebarRow WorktreeRow(RepositoryState tab, WorktreeInfo worktree)
    {
        var branch = worktree.IsBare
            ? "bare"
            : worktree.BranchFullName is { } fullName
                ? fullName.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal) ? fullName[GitRefPrefixes.Heads.Length..] : fullName
                : DetachedText;
        var annotation = branch
            + (worktree.IsLocked ? " locked" : string.Empty)
            + (worktree.IsPrunable ? " prunable" : string.Empty);
        var isCurrent = RepositoryPathComparer.Instance.Equals(worktree.Path, tab.CommandPath);
        var changes = WorktreeChanges(tab, worktree, isCurrent);
        return new SidebarRow(
            SidebarRowKind.Worktree,
            SidebarSection.Worktrees,
            DirectoryName(worktree.Path),
            annotation,
            worktree.HeadHash,
            isCurrent,
            false,
            0,
            changes?.Ahead,
            changes?.Behind,
            worktree.Path,
            changes);
    }

    // The tab's own worktree is counted from the status the tab keeps current; the others from the last overview read.
    private static WorktreeStatus? WorktreeChanges(RepositoryState tab, WorktreeInfo worktree, bool isCurrent)
    {
        if (isCurrent)
        {
            return tab.WorkingTree is { } tree ? WorktreeStatus.From(tree.Snapshot) : null;
        }

        return tab.WorktreeStatuses is { } statuses && statuses.TryGetValue(worktree.Path, out var status)
            ? status
            : null;
    }

    private static SidebarRow StashRow(StashEntry stash) =>
        new(SidebarRowKind.Stash, SidebarSection.Stashes, stash.Name, stash.Message, stash.CommitHash, false, false, 0, null, null);

    private static bool Matches(SidebarRow row, string filter) =>
        filter.Length == 0
        || row.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || row.Annotation.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static string DirectoryName(string path)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        var name = Path.GetFileName(trimmed);
        return name.Length == 0 ? trimmed : name;
    }
}
