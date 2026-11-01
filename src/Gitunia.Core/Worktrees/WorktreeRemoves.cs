using System.Collections.Immutable;
using Gitunia.Core.Effects;

namespace Gitunia.Core.Worktrees;

/// <summary>
/// Removing worktrees and pruning their records. Nothing is removed unless the first dialog gets <c>y</c>; when the
/// worktree then turns out to hold uncommitted changes, a second dialog lists the loss and the removal waits for its
/// <c>y</c> too.
/// </summary>
internal static class WorktreeRemoves
{
    /// <summary>Opens the first confirm, or refuses without one.</summary>
    public static ImmutableArray<AppEffect> Request(WorktreeTarget target, WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [WorktreeReducer.Warn(WorktreeTexts.BusyNotice)];
        }

        if (WorktreeResolver.OfRow(context.Tab, target) is not { } worktree)
        {
            return [WorktreeReducer.Warn(WorktreeTexts.NoWorktreeNotice)];
        }

        var refusal = WorktreeTexts.RemoveRefusal(worktree, context.Tab.Location.RootPath);
        if (refusal.Length > 0)
        {
            return [WorktreeReducer.Warn(refusal)];
        }

        var accepted = new WorktreeRemoveConfirmed(context.Tab.Id, worktree.Path);

        return [new ShowConfirmEffect(WorktreeTexts.RemovePrompt(worktree), accepted, WorktreeTexts.TooSmallNotice)];
    }

    /// <summary>Starts the removal of the worktree the user confirmed.</summary>
    public static ImmutableArray<AppEffect> Run(string path, bool force, WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [WorktreeReducer.Warn($"{WorktreeTexts.BusyNotice}; nothing was removed")];
        }

        return [new RemoveWorktreeEffect(context.Tab.Id, context.Tab.Location, path, force)];
    }

    /// <summary>Asks the second question, with the changes the removal would lose.</summary>
    public static ImmutableArray<AppEffect> AskForce(WorktreeRemoveNeedsForce needsForce)
    {
        var prompt = WorktreeTexts.DirtyRemovePrompt(needsForce.Path, needsForce.Changes);
        var accepted = new WorktreeForceRemoveConfirmed(needsForce.TabId, needsForce.Path);
        var unavailable = $"{needsForce.Path} has changes and the screen is too small to ask: nothing was removed";

        return [new ShowConfirmEffect(prompt, accepted, unavailable)];
    }

    /// <summary>Opens the prune confirm that lists the entries, or refuses when there are none.</summary>
    public static ImmutableArray<AppEffect> RequestPrune(WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [WorktreeReducer.Warn(WorktreeTexts.BusyNotice)];
        }

        var prunable = context.Tab.Worktrees.Where(worktree => worktree.IsPrunable && !worktree.IsLocked).ToList();
        if (prunable.Count == 0)
        {
            return [WorktreeReducer.Warn("No worktree entry to prune")];
        }

        var accepted = new WorktreePruneConfirmed(context.Tab.Id);

        return [new ShowConfirmEffect(WorktreeTexts.PrunePrompt(prunable), accepted, WorktreeTexts.TooSmallNotice)];
    }
}
