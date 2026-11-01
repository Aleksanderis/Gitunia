using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Worktrees.Models;

namespace Gitunia.Core.Worktrees;

/// <summary>
/// Add, remove, lock, unlock, prune, switch to and open worktrees (ux-workflows.md, workflow 6), as pure transitions
/// <c>(WorktreeCommand, WorktreeContext) -> WorktreeUpdate</c>. A new worktree asks for a branch and then a path, a
/// removal goes through the confirm dialog (a second one that lists the changes a dirty worktree would lose), a prune
/// through one that lists the entries. Refusals run no git and say why. What an action acts on is fixed in the prompt
/// or confirm result when it opens. One operation runs per tab.
/// </summary>
public static class WorktreeReducer
{
    /// <summary>Applies a command and returns the effects to run.</summary>
    public static WorktreeUpdate Reduce(WorktreeCommand command, WorktreeContext context) => command switch
    {
        WorktreeAddRequested request => BeginAdd(request.Target, context),
        WorktreeBranchAccepted accepted => BranchAccepted(accepted, context),
        WorktreeForceRemoveConfirmed confirmed => WorktreeRemoves.Run(confirmed.Path, true, context),
        WorktreeLocated located => WorktreeSwitches.Located(located, context),
        WorktreeLocateFailed failed => Failure(failed.Text),
        WorktreeLockReasonAccepted accepted => LockAccepted(accepted, context),
        WorktreeLockRequested request => BeginLock(request.Target, context),
        WorktreeOpenRequested request => Open(request.Target, context),
        WorktreeOperationCancelled => new WorktreeUpdate([]),
        WorktreeOperationFailed failed => Failed(failed, context),
        WorktreeOperationSucceeded succeeded => Succeeded(succeeded, context),
        WorktreePathAccepted accepted => PathAccepted(accepted, context),
        WorktreePruneConfirmed => Prune(context),
        WorktreePruneRequested => WorktreeRemoves.RequestPrune(context),
        WorktreeRemoveConfirmed confirmed => WorktreeRemoves.Run(confirmed.Path, false, context),
        WorktreeRemoveNeedsForce needsForce => WorktreeRemoves.AskForce(needsForce),
        WorktreeRemoveRequested request => WorktreeRemoves.Request(request.Target, context),
        WorktreeStatusesRead read => WorktreeSwitches.StatusesRead(read, context),
        WorktreeSwitchRequested request => WorktreeSwitches.Begin(request.Target, context, true),
        WorktreeUnlockRequested request => Unlock(request.Target, context),
        _ => throw new UnhandledCommandException(command),
    };

    /// <summary>A warning status, the way a refusal is told.</summary>
    internal static PostStatusEffect Warn(string text) => new(text, StatusSeverity.Warning);

    private static ImmutableArray<AppEffect> BeginAdd(WorktreeTarget target, WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [Warn(WorktreeTexts.BusyNotice)];
        }

        // A selected worktree or branch row wins over the graph selection; without either git starts at HEAD.
        var start = target.GraphCommit;
        if (target is { RowKind: not SidebarRowKind.Stash, RowCommitHash: { } hash })
        {
            start = hash;
        }

        var request = new PromptRequest(
            "New worktree",
            "Branch: an existing one, or a new name",
            "next",
            new WorktreeBranchAnswer(context.Tab.Id, start),
            Owner: context.Tab.Id);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> Open(WorktreeTarget target, WorktreeContext context)
    {
        // A local branch that no worktree holds is left to the add flow, which asks for the path only.
        if (target is { RowKind: SidebarRowKind.LocalBranch, Label: { } name }
            && WorktreeResolver.HolderOf(context.Tab, name) is null)
        {
            return BeginAddForBranch(name, context);
        }

        return WorktreeSwitches.Begin(target, context, false);
    }

    private static ImmutableArray<AppEffect> BeginAddForBranch(string name, WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [Warn(WorktreeTexts.BusyNotice)];
        }

        return [PathPrompt(context, name, null, true)];
    }

    private static ImmutableArray<AppEffect> BranchAccepted(WorktreeBranchAccepted accepted, WorktreeContext context)
    {
        var name = accepted.Name.Trim();
        if (name.Length == 0)
        {
            return [Warn("No branch given: nothing was added")];
        }

        var tab = context.Tab;
        var existing = tab.Refs.FirstOrDefault(
            candidate => candidate.Kind == RefKind.LocalBranch && candidate.Name == name);
        if (existing?.CheckedOutIn is { } holder)
        {
            return [Warn($"{name} is already checked out in {holder}: a branch can be in one worktree only")];
        }

        var start = existing is null ? accepted.SelectedStart ?? tab.Head?.CommitHash ?? GitOptions.HeadRevision : null;

        return [PathPrompt(context, name, start, false)];
    }

    private static ShowPromptEffect PathPrompt(WorktreeContext context, string branch, string? start, bool openAfter)
    {
        var tab = context.Tab;
        var startText = start is null ? " (existing)" : $" (new, at {ShortHash.Of(start)})";
        var request = new PromptRequest(
            "Worktree path",
            $"Directory for {branch}{startText}",
            "add",
            new WorktreePathAnswer(tab.Id, branch, start, openAfter),
            WorktreeTexts.DefaultPath(tab.Location.RootPath, branch),
            Owner: tab.Id);

        return new ShowPromptEffect(request);
    }

    private static ImmutableArray<AppEffect> PathAccepted(WorktreePathAccepted accepted, WorktreeContext context)
    {
        var text = accepted.Text.Trim();
        if (text.Length == 0)
        {
            return [Warn("No path given: nothing was added")];
        }

        if (context.IsBusy)
        {
            return [Warn($"{WorktreeTexts.BusyNotice}; nothing was changed")];
        }

        var root = context.Tab.Location.RootPath;
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root)) ?? root;
        var path = Path.GetFullPath(text, parent);
        var request = new WorktreeAddRequest(path, accepted.Branch, accepted.StartPoint);

        return [new AddWorktreeEffect(context.Tab.Id, context.Tab.Location, request, accepted.OpenAfter)];
    }

    private static ImmutableArray<AppEffect> BeginLock(WorktreeTarget target, WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [Warn(WorktreeTexts.BusyNotice)];
        }

        if (WorktreeResolver.OfRow(context.Tab, target) is not { } worktree)
        {
            return [Warn(WorktreeTexts.NoWorktreeNotice)];
        }

        if (worktree.IsMain)
        {
            return [Warn("The main worktree cannot be locked")];
        }

        if (worktree.IsLocked)
        {
            return [Warn($"{worktree.Path} is already locked")];
        }

        var request = new PromptRequest(
            "Lock worktree",
            $"Reason for locking {worktree.Path}, optional",
            "lock",
            new WorktreeLockAnswer(context.Tab.Id, worktree.Path),
            Owner: context.Tab.Id);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> LockAccepted(WorktreeLockReasonAccepted accepted, WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [Warn($"{WorktreeTexts.BusyNotice}; nothing was changed")];
        }

        return [new LockWorktreeEffect(context.Tab.Id, context.Tab.Location, accepted.Path, accepted.Reason.Trim())];
    }

    private static ImmutableArray<AppEffect> Unlock(WorktreeTarget target, WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [Warn(WorktreeTexts.BusyNotice)];
        }

        if (WorktreeResolver.OfRow(context.Tab, target) is not { } worktree)
        {
            return [Warn(WorktreeTexts.NoWorktreeNotice)];
        }

        if (!worktree.IsLocked)
        {
            return [Warn($"{worktree.Path} is not locked")];
        }

        return [new UnlockWorktreeEffect(context.Tab.Id, context.Tab.Location, worktree.Path)];
    }

    private static ImmutableArray<AppEffect> Prune(WorktreeContext context)
    {
        if (context.IsBusy)
        {
            return [Warn($"{WorktreeTexts.BusyNotice}; nothing was changed")];
        }

        return [new PruneWorktreesEffect(context.Tab.Id, context.Tab.Location)];
    }

    private static ImmutableArray<AppEffect> Succeeded(WorktreeOperationSucceeded succeeded, WorktreeContext context)
    {
        var effects = ImmutableArray.CreateBuilder<AppEffect>();
        effects.Add(new PostStatusEffect(succeeded.Text, StatusSeverity.Info));
        effects.Add(new RefreshTabEffect(context.Tab.Id));
        if (succeeded.OpenPath is { } path)
        {
            effects.Add(new LocateWorktreeEffect(context.Tab.Id, path, false));
        }

        return effects.ToImmutable();
    }

    private static ImmutableArray<AppEffect> Failed(WorktreeOperationFailed failed, WorktreeContext context) =>
        [new PostStatusEffect(failed.Text, StatusSeverity.Error), new RefreshTabEffect(context.Tab.Id)];

    private static ImmutableArray<AppEffect> Failure(string text) => [new PostStatusEffect(text, StatusSeverity.Error)];
}
