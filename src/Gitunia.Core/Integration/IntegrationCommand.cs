using System.Collections.Immutable;
using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Merge;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Integration;

/// <summary>A reset, merge or operation action or the result of one, for one tab. Dropped when the tab is no longer
/// open (a late result).</summary>
public abstract record IntegrationCommand : AppCommand
{
    private protected IntegrationCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Reset the current branch to a commit: opens the mode prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="CommitHash">The commit the user selected, null when the selection is no commit, branch or
/// tag.</param>
public sealed record ResetRequested(TabId TabId, string? CommitHash) : IntegrationCommand(TabId);

/// <summary>A reset mode key was pressed in the mode prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">The commit, branch and mode the user chose.</param>
public sealed record ResetModeChosen(TabId TabId, ResetTarget Target) : IntegrationCommand(TabId);

/// <summary><c>y</c> in the reset confirm.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">The reset the dialog named.</param>
public sealed record ResetConfirmed(TabId TabId, ResetTarget Target) : IntegrationCommand(TabId);

/// <summary>Merge the selected commit, branch or tag into the current branch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Mode">Fast-forward when possible, always a merge commit, or squash.</param>
/// <param name="Target">What the user selected, null when the selection is no commit, branch or tag.</param>
public sealed record MergeRequested(TabId TabId, MergeMode Mode, MergeTarget? Target) : IntegrationCommand(TabId);

/// <summary>Continue, abort or skip the operation in progress.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Action">What to do.</param>
public sealed record OperationRequested(TabId TabId, OperationAction Action) : IntegrationCommand(TabId);

/// <summary><c>y</c> in the abort confirm.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Info">The operation the dialog named.</param>
/// <param name="Branch">The branch it runs on.</param>
public sealed record OperationAbortConfirmed(TabId TabId, OperationInfo Info, string? Branch)
    : IntegrationCommand(TabId);

/// <summary><c>y</c> in the skip confirm.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Info">The operation and commit the dialog named.</param>
public sealed record OperationSkipConfirmed(TabId TabId, OperationInfo Info) : IntegrationCommand(TabId);

/// <summary>Result: what the reset would drop was read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">The reset.</param>
/// <param name="Operation">The operation the worktree files show as stopped, <c>None</c> when there is none.</param>
/// <param name="Changes">The tracked files with changes; empty for a soft reset, which does not read them.</param>
public sealed record ResetPrepared(
    TabId TabId,
    ResetTarget Target,
    RepositoryOperation Operation,
    ImmutableArray<StatusEntry> Changes) : IntegrationCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Integration;
}

/// <summary>Result: a call finished; the tab is read again.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Detail">The undo command, kept in the message history; null when there is none.</param>
public sealed record IntegrationSucceeded(TabId TabId, string Text, StatusSeverity Severity, string? Detail = null)
    : IntegrationCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Integration;
}

/// <summary>Result: a call failed.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
/// <param name="Refresh">Read the tab again, because git may have done part of it; false when nothing was
/// changed.</param>
/// <param name="Output">Git's whole output for the output box, null when the status line says it all.</param>
public sealed record IntegrationFailed(TabId TabId, string Text, bool Refresh = true, FailureOutput? Output = null)
    : IntegrationCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Integration;
}

/// <summary>Result: the call was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record IntegrationCancelled(TabId TabId) : IntegrationCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Integration;
}
