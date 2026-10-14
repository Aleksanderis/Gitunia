using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Workspaces;

/// <summary>
/// The open repository tabs in order, which one is active, and the recent and pinned repository paths.
/// An empty workspace (no tabs) is what the repository picker is shown for.
/// </summary>
/// <param name="Tabs">Open tabs in display order.</param>
/// <param name="ActiveTabId">The focused tab, null when there are no tabs.</param>
/// <param name="PreviousTabId">The tab that was active before the current one, for the "previous tab" key.</param>
/// <param name="Recents">Root paths of recently opened repositories, most recent first.</param>
/// <param name="Pins">Root paths of pinned repositories in pin order.</param>
/// <param name="NextTabNumber">The number the next opened tab gets.</param>
public sealed record Workspace(
    ImmutableArray<RepositoryState> Tabs,
    TabId? ActiveTabId,
    TabId? PreviousTabId,
    ImmutableArray<string> Recents,
    ImmutableArray<string> Pins,
    int NextTabNumber)
{
    /// <summary>The most recent paths kept.</summary>
    public const int MaximumRecents = 50;

    /// <summary>A workspace with no tabs, recents or pins.</summary>
    public static Workspace Empty { get; } = new([], null, null, [], [], 1);

    /// <summary>The active tab, or null when there is none.</summary>
    public RepositoryState? ActiveTab => ActiveTabId is { } activeId ? FindTab(activeId) : null;

    /// <summary>Finds a tab by id, or null when it is not open.</summary>
    public RepositoryState? FindTab(TabId id)
    {
        foreach (var tab in Tabs)
        {
            if (tab.Id == id)
            {
                return tab;
            }
        }

        return null;
    }

    /// <summary>Finds the tab showing the repository with this root path, or null.</summary>
    public RepositoryState? FindTabByRootPath(string rootPath)
    {
        foreach (var tab in Tabs)
        {
            if (RepositoryPathComparer.Instance.Equals(tab.Location.RootPath, rootPath))
            {
                return tab;
            }
        }

        return null;
    }

    /// <summary>Whether the repository with this root path is pinned.</summary>
    public bool IsPinned(string rootPath) => Pins.Contains(rootPath, RepositoryPathComparer.Instance);
}
