using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Worktrees;

namespace Gitunia.Core.Worktrees;

/// <summary>
/// Moving between worktrees (ux-workflows.md, workflow 6): a switch points the tab at the worktree in place, an open
/// gives it its own tab. Refusals that need no git are decided here from the tab's worktree list; the path is then
/// resolved by the handler and the tab is checked again when the location arrives, because the workspace may have
/// changed meanwhile.
/// </summary>
internal static class WorktreeSwitches
{
    /// <summary>Starts a switch or an open of the selected worktree.</summary>
    public static ImmutableArray<AppEffect> Begin(WorktreeTarget target, WorktreeContext context, bool isSwitch)
    {
        if (isSwitch && context.IsBusy)
        {
            return [WorktreeReducer.Warn(WorktreeTexts.BusyNotice)];
        }

        if (WorktreeResolver.OfRowOrBranch(context.Tab, target) is not { } worktree)
        {
            return [WorktreeReducer.Warn(WorktreeTexts.NoWorktreeNotice)];
        }

        var refusal = Refusal(context, worktree, isSwitch);
        if (refusal.Length > 0)
        {
            return [WorktreeReducer.Warn(refusal)];
        }

        return [new LocateWorktreeEffect(context.Tab.Id, worktree.Path, isSwitch)];
    }

    /// <summary>Applies the resolved location: opens a tab, or points the tab at it unless that became
    /// impossible.</summary>
    public static WorktreeUpdate Located(WorktreeLocated located, WorktreeContext context)
    {
        var root = located.Location.RootPath;
        if (!located.IsSwitch)
        {
            return new WorktreeUpdate(
                [new PostStatusEffect($"Opened worktree {root}", StatusSeverity.Info)],
                new OpenRepositoryCommand(located.Location));
        }

        if (context.IsBusy)
        {
            return new WorktreeUpdate(
                [WorktreeReducer.Warn($"{WorktreeTexts.BusyNotice}; the tab stays on {context.Tab.Location.RootPath}")]);
        }

        if (OtherTabShowing(context, root) is { } other)
        {
            var text = $"{root} is already open in tab {other.Title}: the tab stays where it is";

            return new WorktreeUpdate([WorktreeReducer.Warn(text)]);
        }

        return new WorktreeUpdate(
            [new PostStatusEffect($"Switched to worktree {root}", StatusSeverity.Info)],
            new SwitchWorktreeCommand(context.Tab.Id, located.Location));
    }

    /// <summary>The overview of the other worktrees was read: the tab takes it unless it moved or nothing
    /// changed.</summary>
    public static WorktreeUpdate StatusesRead(WorktreeStatusesRead read, WorktreeContext context)
    {
        var tab = context.Tab;
        if (tab.Location != read.Location || SameStatuses(tab.WorktreeStatuses, read))
        {
            return new WorktreeUpdate([]);
        }

        return new WorktreeUpdate([], new TabCommand(tab.Id, new WorktreeStatusesLoadedCommand(read.Statuses)));
    }

    private static string Refusal(WorktreeContext context, WorktreeInfo worktree, bool isSwitch)
    {
        if (worktree.IsBare)
        {
            return "The bare repository has no working tree to switch to";
        }

        if (worktree.IsPrunable)
        {
            return $"{worktree.Path} is gone from disk: prune it with <space>wp";
        }

        if (RepositoryPathComparer.Instance.Equals(worktree.Path, context.Tab.CommandPath))
        {
            return "This tab already shows that worktree";
        }

        if (isSwitch && OtherTabShowing(context, worktree.Path) is { } other)
        {
            return $"{worktree.Path} is already open in tab {other.Title}: <space>wt goes there";
        }

        return string.Empty;
    }

    private static RepositoryState? OtherTabShowing(WorktreeContext context, string path) =>
        context.Tabs.FirstOrDefault(other => other.Id != context.Tab.Id
            && RepositoryPathComparer.Instance.Equals(other.Location.RootPath, path));

    private static bool SameStatuses(
        ImmutableDictionary<string, WorktreeStatus>? current,
        WorktreeStatusesRead read) =>
        current is not null
        && current.Count == read.Statuses.Count
        && read.Statuses.All(pair => current.TryGetValue(pair.Key, out var existing) && existing == pair.Value);

}
