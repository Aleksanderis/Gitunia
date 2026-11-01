using System.Collections.Immutable;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Pick;

namespace Gitunia.Core.Pick;

/// <summary>A cherry-pick or revert action or the result of one, for one tab. Dropped when the tab is gone.</summary>
public abstract record PickCommand : AppCommand
{
    private protected PickCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Cherry-pick (<c>gp</c>) or revert (<c>gv</c>) what is selected onto the current branch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="PickKind">Cherry-pick or revert.</param>
/// <param name="Target">What is selected.</param>
public sealed record PickRequested(TabId TabId, PickKind PickKind, PickTarget Target) : PickCommand(TabId);

/// <summary>A key or a click answered the mainline question.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Parent">The parent number the user chose.</param>
public sealed record PickMainlineChosen(TabId TabId, int Parent) : PickCommand(TabId);

/// <summary>The mainline question was cancelled.</summary>
/// <param name="TabId">The tab.</param>
public sealed record PickMainlineCancelled(TabId TabId) : PickCommand(TabId);

/// <summary>Result: the commits were read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="PickKind">Cherry-pick or revert.</param>
/// <param name="Commits">The commits in apply order.</param>
public sealed record PickCommitsRead(TabId TabId, PickKind PickKind, ImmutableArray<PickCommit> Commits)
    : PickCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Pick;
}

/// <summary>Result: the commits could not be read; nothing was done.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record PickReadFailed(TabId TabId, string Text) : PickCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Pick;
}

/// <summary>Result: git ran; it completed, had nothing to apply or stopped on conflicts.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="PickKind">Cherry-pick or revert.</param>
/// <param name="Result">What git did.</param>
/// <param name="Target">How the branch is named in messages.</param>
/// <param name="What">How the commits are named in messages.</param>
public sealed record PickFinished(TabId TabId, PickKind PickKind, PickResult Result, string Target, string What)
    : PickCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Pick;
}

/// <summary>Result: the operation failed; the tab is refreshed anyway because git may have done part of it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
public sealed record PickFailed(TabId TabId, string Text) : PickCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Pick;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record PickCancelled(TabId TabId) : PickCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Pick;
}
