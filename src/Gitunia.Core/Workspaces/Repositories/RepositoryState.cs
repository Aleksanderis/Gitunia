using System.Collections.Immutable;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Worktrees;

namespace Gitunia.Core.Workspaces.Repositories;

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
/// <param name="Status">Dirty and ahead/behind facts for the tab bar, null until loaded.</param>
/// <param name="WorkingTree">The full status and its selection, null until the tab has loaded one (background tabs keep only <paramref name="Status"/>).</param>
/// <param name="CustomName">The name the user gave the tab (#219), null to show the repository name.</param>
/// <param name="WorktreeStatuses">The last status read of the other worktrees by path (sidebar overview), null until
/// read.</param>
/// <param name="Operation">The merge, rebase, cherry-pick or revert that is stopped in this tab, null when none
/// (#37).</param>
/// <param name="RebaseAutoStash">The user's <c>rebase.autoStash</c>: git carries uncommitted changes over a rebase, so
/// the guard does not refuse a dirty tree.</param>
public sealed record RepositoryState(
    TabId Id,
    RepositoryLocation Location,
    HeadState? Head,
    ImmutableArray<GitRef> Refs,
    RepositoryViewState View,
    ImmutableArray<WorktreeInfo> Worktrees,
    ImmutableArray<StashEntry> Stashes,
    SidebarState Sidebar,
    bool IsMissing,
    RepositoryStatusSummary? Status = null,
    string? CustomName = null,
    WorkingTreeState? WorkingTree = null,
    ImmutableDictionary<string, WorktreeStatus>? WorktreeStatuses = null,
    OperationInfo? Operation = null,
    bool RebaseAutoStash = false)
{
    /// <summary>The operations in flight for this tab; the root reducer keeps them (tab eviction and busy refusals read them).</summary>
    public RunningOperations Running { get; init; } = RunningOperations.Empty;

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

    /// <summary>Longest tab name; longer names are cut so one tab cannot take the whole tab row.</summary>
    public const int MaximumNameLength = 40;

    /// <summary>A name as stored: trimmed, cut to <see cref="MaximumNameLength"/> without splitting a surrogate pair, null when blank.</summary>
    public static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > MaximumNameLength)
        {
            var length = char.IsHighSurrogate(trimmed[MaximumNameLength - 1]) ? MaximumNameLength - 1 : MaximumNameLength;
            trimmed = trimmed[..length].TrimEnd();
        }

        return trimmed;
    }

    /// <summary>What the tab bar calls the tab: the name the user gave it, else the repository name.</summary>
    public string Title => CustomName ?? DisplayName;

    /// <summary>A newly opened tab with nothing loaded yet.</summary>
    public static RepositoryState Create(TabId id, RepositoryLocation location) =>
        new(id, location, null, [], RepositoryViewState.Initial, [], [], SidebarState.Initial, false);
}
