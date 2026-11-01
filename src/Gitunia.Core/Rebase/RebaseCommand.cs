using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;

namespace Gitunia.Core.Rebase;

/// <summary>A rebase or quick rebase action or the result of one, for one tab. Dropped when the tab is gone.</summary>
public abstract record RebaseCommand : AppCommand
{
    private protected RebaseCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Rebase the current branch onto the selected commit, branch or tag: counts the merges, then asks.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Selection">What is selected.</param>
public sealed record RebaseOntoRequested(TabId TabId, RebaseSelection Selection) : RebaseCommand(TabId);

/// <summary><c>y</c> in the rebase confirm; the target is what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What the rebase acts on.</param>
public sealed record RebaseOntoConfirmed(TabId TabId, RebaseTarget Target) : RebaseCommand(TabId);

/// <summary>Fixup, reword or drop the commit selected in the graph: plans it, then asks.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Kind">Fixup, reword or drop.</param>
/// <param name="CommitHash">The commit selected in the graph, null when none.</param>
public sealed record QuickRebaseRequested(TabId TabId, QuickRebaseKind Kind, string? CommitHash) : RebaseCommand(TabId);

/// <summary>The reword subject prompt was accepted.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Plan">The plan the prompt was opened for.</param>
/// <param name="Text">The typed subject.</param>
public sealed record QuickRebaseSubjectAccepted(TabId TabId, QuickRebasePlan Plan, string Text) : RebaseCommand(TabId);

/// <summary><c>y</c> in the quick rebase confirm; the target is what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What the quick rebase acts on.</param>
public sealed record QuickRebaseConfirmed(TabId TabId, QuickRebaseTarget Target) : RebaseCommand(TabId);

/// <summary>Result: the merge commits of the range were counted (zero when git could not tell).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What the rebase acts on.</param>
/// <param name="Merges">How many merge commits get flattened.</param>
public sealed record RebaseMergesCounted(TabId TabId, RebaseTarget Target, int Merges)
    : RebaseCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}

/// <summary>Result: the quick rebase was planned.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Plan">The commit, its range and the HEAD it was read at.</param>
public sealed record QuickRebasePlanned(TabId TabId, QuickRebasePlan Plan) : RebaseCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}

/// <summary>Result: the quick rebase could not be planned; nothing was done.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record QuickRebasePlanFailed(TabId TabId, string Text) : RebaseCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}

/// <summary>Result: the rebase ran; it finished or stopped on conflicts.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What the rebase acted on.</param>
/// <param name="Result">What git did.</param>
public sealed record RebaseFinished(TabId TabId, RebaseTarget Target, OperationResult Result)
    : RebaseCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}

/// <summary>Result: the quick rebase ran; it finished or stopped on conflicts.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What the quick rebase acted on.</param>
/// <param name="Result">What git did.</param>
public sealed record QuickRebaseFinished(TabId TabId, QuickRebaseTarget Target, InteractiveRebaseResult Result)
    : RebaseCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}

/// <summary>Result: the rebase failed; the tab is refreshed anyway because git may have done part of it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
public sealed record RebaseFailed(TabId TabId, string Text) : RebaseCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record RebaseCancelled(TabId TabId) : RebaseCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}
