using System.Collections.Immutable;
using Gitunia.Core.Workspaces;
using Gitunia.Git;

namespace Gitunia.Core.Persistence;

/// <summary>Converts between the live <see cref="Workspace"/> and the session file contents. Pure; reading is tolerant of bad entries.</summary>
public static class SessionDocumentMapper
{
    /// <summary>Snapshots the workspace, including tabs whose repository is currently missing.</summary>
    public static SessionDocument ToDocument(Workspace workspace)
    {
        var tabs = new List<SessionTabDocument>(workspace.Tabs.Length);
        int? activeIndex = null;
        for (var index = 0; index < workspace.Tabs.Length; index++)
        {
            var tab = workspace.Tabs[index];
            if (tab.Id == workspace.ActiveTabId)
            {
                activeIndex = index;
            }

            tabs.Add(new SessionTabDocument
            {
                WorkingTreePath = tab.Location.WorkingTreePath,
                GitDirectory = tab.Location.GitDirectory,
                CommonDirectory = tab.Location.CommonDirectory,
                IsBare = tab.Location.IsBare,
                IsLinkedWorktree = tab.Location.IsLinkedWorktree,
                SelectedCommit = tab.View.SelectedCommit?.ToString(),
                ScrollOffset = tab.View.ScrollOffset,
                FilterText = tab.View.FilterText,
                // The diff pane is not open after a restart, so a tab that had it focused comes back on the file list.
                FocusedPane = (tab.View.FocusedPane == PaneKind.Diff ? PaneKind.Detail : tab.View.FocusedPane).ToString(),
                SidebarWidth = tab.View.SidebarWidth,
                BottomHeight = tab.View.BottomHeight,
                DetailWidth = tab.View.DetailWidth,
            });
        }

        return new SessionDocument
        {
            Version = SessionDocument.CurrentVersion,
            ActiveTabIndex = activeIndex,
            Tabs = tabs,
            Recents = [.. workspace.Recents],
            Pins = [.. workspace.Pins],
        };
    }

    /// <summary>
    /// Rebuilds a workspace from a document. Unusable entries (relative or empty paths, duplicates) are skipped and bad
    /// values fall back to defaults. Tabs whose directory is gone are kept and flagged missing; the others get a load effect.
    /// </summary>
    /// <param name="document">The parsed file.</param>
    /// <param name="directoryExists">Whether a directory exists; injected so the mapper stays pure.</param>
    public static WorkspaceUpdate ToWorkspace(SessionDocument document, Func<string, bool> directoryExists)
    {
        var tabs = ImmutableArray.CreateBuilder<RepoState>();
        var effects = ImmutableArray.CreateBuilder<WorkspaceEffect>();
        TabId? activeId = null;
        var nextNumber = 1;
        var documentTabs = document.Tabs ?? [];
        for (var index = 0; index < documentTabs.Count; index++)
        {
            var location = ReadLocation(documentTabs[index]);
            if (location is null || tabs.Any(existing => RepositoryPathComparer.Instance.Equals(existing.Location.RootPath, location.RootPath)))
            {
                continue;
            }

            var id = new TabId(nextNumber++);
            var isMissing = !directoryExists(location.CommandPath);
            var tab = RepoState.Create(id, location) with { View = ReadView(documentTabs[index]), IsMissing = isMissing };
            tabs.Add(tab);
            if (!isMissing)
            {
                effects.Add(new LoadRepositoryEffect(id, tab.CommandPath));
            }

            if (document.ActiveTabIndex == index)
            {
                activeId = id;
            }
        }

        activeId ??= tabs.Count > 0 ? tabs[0].Id : null;
        var workspace = new Workspace(
            tabs.ToImmutable(),
            activeId,
            null,
            ReadPaths(document.Recents, Workspace.MaximumRecents),
            ReadPaths(document.Pins, int.MaxValue),
            nextNumber);
        return new WorkspaceUpdate(workspace, effects.ToImmutable());
    }

    private static RepositoryLocation? ReadLocation(SessionTabDocument tab)
    {
        if (!IsAbsolute(tab.GitDirectory) || (tab.WorkingTreePath is not null && !IsAbsolute(tab.WorkingTreePath)))
        {
            return null;
        }

        if (!tab.IsBare && tab.WorkingTreePath is null)
        {
            return null;
        }

        var commonDirectory = IsAbsolute(tab.CommonDirectory) ? tab.CommonDirectory! : tab.GitDirectory!;
        return new RepositoryLocation(tab.IsBare ? null : tab.WorkingTreePath, tab.GitDirectory!, commonDirectory, tab.IsBare, tab.IsLinkedWorktree);
    }

    private static RepoViewState ReadView(SessionTabDocument tab)
    {
        ObjectId? selected = ObjectId.TryParse(tab.SelectedCommit ?? string.Empty, out var objectId) ? objectId : null;
        var pane = Enum.TryParse<PaneKind>(tab.FocusedPane, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : PaneKind.Graph;
        return new RepoViewState(selected, Math.Max(0, tab.ScrollOffset), tab.FilterText ?? string.Empty, pane, PositiveOrNull(tab.SidebarWidth), PositiveOrNull(tab.BottomHeight), PositiveOrNull(tab.DetailWidth));
    }

    // The layout clamps to what fits; here only nonsense (zero, negative) falls back to automatic.
    private static int? PositiveOrNull(int? value) => value is > 0 ? value : null;

    private static ImmutableArray<string> ReadPaths(List<string>? paths, int limit)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        foreach (var path in paths ?? [])
        {
            if (result.Count >= limit)
            {
                break;
            }

            if (IsAbsolute(path) && !result.Contains(path, RepositoryPathComparer.Instance))
            {
                result.Add(path);
            }
        }

        return result.ToImmutable();
    }

    private static bool IsAbsolute(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && !path.Contains('\0');
}
