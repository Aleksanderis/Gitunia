using System.Collections.Immutable;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Persistence;

/// <summary>Converts between the live <see cref="Workspace"/> and the session file contents. Pure; reading is tolerant of bad entries.</summary>
public static class SessionDocumentMapper
{
    /// <summary>Snapshots the workspace, including tabs whose repository is currently missing.</summary>
    /// <param name="workspace">The workspace to save.</param>
    /// <param name="drafts">The commit drafts by tab id; null saves none.</param>
    public static SessionDocument ToDocument(
        Workspace workspace,
        IReadOnlyDictionary<TabId, SessionDraft>? drafts = null)
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

            var draft = FindDraft(drafts, tab.Id);
            tabs.Add(new SessionTabDocument
            {
                DraftMessage = draft?.Message,
                DraftSignOff = draft?.SignOff ?? false,
                DraftNoVerify = draft?.NoVerify ?? false,
                WorkingTreePath = tab.Location.WorkingTreePath,
                GitDirectory = tab.Location.GitDirectory,
                CommonDirectory = tab.Location.CommonDirectory,
                IsBare = tab.Location.IsBare,
                IsLinkedWorktree = tab.Location.IsLinkedWorktree,
                SelectedCommit = tab.View.SelectedCommit?.ToString(),
                ScrollOffset = tab.View.ScrollOffset,
                FilterText = tab.View.FilterText,
                FocusedPane = SavedPane(tab.View.FocusedPane).ToString(),
                SidebarWidth = tab.View.SidebarWidth,
                BottomHeight = tab.View.BottomHeight,
                DetailWidth = tab.View.DetailWidth,
                BottomTab = tab.View.BottomTab.ToString(),
                CustomName = tab.CustomName,
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
    public static WorkspaceUpdate ToWorkspace(SessionDocument document, Func<string, bool> directoryExists) =>
        ToSession(document, directoryExists).Update;

    /// <summary>Like <see cref="ToWorkspace"/>, and also returns the saved commit drafts under the new tab ids.</summary>
    /// <param name="document">The parsed file.</param>
    /// <param name="directoryExists">Whether a directory exists; injected so the mapper stays pure.</param>
    public static RestoredSession ToSession(SessionDocument document, Func<string, bool> directoryExists)
    {
        var drafts = ImmutableDictionary.CreateBuilder<TabId, SessionDraft>();
        var tabs = ImmutableArray.CreateBuilder<RepositoryState>();
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
            var tab = RepositoryState.Create(id, location) with { View = ReadView(documentTabs[index]), IsMissing = isMissing, CustomName = RepositoryState.NormalizeName(documentTabs[index].CustomName) };
            tabs.Add(tab);
            var source = documentTabs[index];
            var saved = new SessionDraft(source.DraftMessage ?? string.Empty, source.DraftSignOff, source.DraftNoVerify);
            if (!saved.IsEmpty)
            {
                drafts[id] = saved;
            }

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
        return new RestoredSession(new WorkspaceUpdate(workspace, effects.ToImmutable()), drafts.ToImmutable());
    }

    private static SessionDraft? FindDraft(IReadOnlyDictionary<TabId, SessionDraft>? drafts, TabId id)
    {
        if (drafts is not null && drafts.TryGetValue(id, out var found) && !found.IsEmpty)
        {
            return found;
        }

        return null;
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

    private static RepositoryViewState ReadView(SessionTabDocument tab)
    {
        ObjectId? selected = ObjectId.TryParse(tab.SelectedCommit ?? string.Empty, out var objectId) ? objectId : null;
        var pane = PaneKind.Graph;
        if (Enum.TryParse<PaneKind>(tab.FocusedPane, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed))
        {
            pane = SavedPane(parsed);
        }

        var bottomTab = RepositoryViewState.Initial.BottomTab;
        if (Enum.TryParse<BottomTab>(tab.BottomTab, ignoreCase: false, out var parsedTab) && Enum.IsDefined(parsedTab))
        {
            bottomTab = parsedTab;
        }

        return new RepositoryViewState(selected, Math.Max(0, tab.ScrollOffset), tab.FilterText ?? string.Empty, pane, PositiveOrNull(tab.SidebarWidth), PositiveOrNull(tab.BottomHeight), PositiveOrNull(tab.DetailWidth), bottomTab);
    }

    // Neither the diff pane nor the working-tree view is open after a restart: a tab that had the diff focused comes back on the
    // file list, and one that was in the working-tree view comes back on the graph.
    private static PaneKind SavedPane(PaneKind pane) => pane switch
    {
        PaneKind.Diff => PaneKind.Detail,
        PaneKind.WorkingUnstaged
            or PaneKind.WorkingStaged
            or PaneKind.WorkingUnstagedDiff
            or PaneKind.WorkingStagedDiff
            or PaneKind.WorkingMessage => PaneKind.Graph,
        _ => pane,
    };

    // The layout clamps to what fits; here only negative values falls back to automatic.
    private static int? PositiveOrNull(int? value) => value is >= 0 ? value : null;

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
