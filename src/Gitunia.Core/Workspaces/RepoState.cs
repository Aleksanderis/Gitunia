using System.Collections.Immutable;
using Gitunia.Core.Sidebar;
using Gitunia.Git;

namespace Gitunia.Core.Workspaces;

/// <summary>Everything one tab knows about its repository. Independent of every other tab.</summary>
/// <param name="Id">The tab id commands are routed by.</param>
/// <param name="Location">The repository this tab shows; its root path is the tab's identity.</param>
/// <param name="Head">What HEAD points at, null until loaded.</param>
/// <param name="Refs">Branches and tags, empty until loaded.</param>
/// <param name="View">Selection, scroll, filter and focus.</param>
/// <param name="Worktrees">The repository's worktrees, empty until loaded.</param>
/// <param name="Stashes">The stash stack, newest first, empty until loaded.</param>
/// <param name="Sidebar">Folded sections, name filter and cursor of the sidebar.</param>
/// <param name="IsMissing">The repository directory disappeared; the tab stays so the user can locate or close it.</param>
public sealed record RepoState(
    TabId Id,
    RepositoryLocation Location,
    HeadState? Head,
    ImmutableArray<GitRef> Refs,
    RepoViewState View,
    ImmutableArray<WorktreeInfo> Worktrees,
    ImmutableArray<StashEntry> Stashes,
    SidebarState Sidebar,
    bool IsMissing)
{
    /// <summary>The path git commands for this tab run against.</summary>
    public string CommandPath => Location.CommandPath;

    /// <summary>The repository name for the tab bar (last path segment); the UI adds the path when names collide.</summary>
    public string DisplayName
    {
        get
        {
            var trimmed = Path.TrimEndingDirectorySeparator(Location.RootPath);
            var name = Path.GetFileName(trimmed);
            return name.Length == 0 ? trimmed : name;
        }
    }

    /// <summary>A newly opened tab with nothing loaded yet.</summary>
    public static RepoState Create(TabId id, RepositoryLocation location) =>
        new(id, location, null, [], RepoViewState.Initial, [], [], SidebarState.Initial, false);
}
