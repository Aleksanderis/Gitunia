using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Branches;

/// <summary>A branch action or the result of one, for one tab. Dropped when the tab is no longer open (a late
/// result).</summary>
public abstract record BranchCommand : AppCommand
{
    private protected BranchCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Create a branch at the selected commit or branch: opens the name prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
/// <param name="Checkout">True to check the new branch out.</param>
public sealed record BranchCreateRequested(TabId TabId, BranchTarget Target, bool Checkout) : BranchCommand(TabId);

/// <summary>Create a branch at a given commit (the reflog list does so for an entry): opens the name prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="CommitHash">The start point.</param>
/// <param name="Checkout">True to check the new branch out.</param>
public sealed record BranchCreateAtRequested(TabId TabId, string CommitHash, bool Checkout) : BranchCommand(TabId);

/// <summary>Check a commit out with a detached HEAD (the reflog list does so for an entry).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="CommitHash">The commit.</param>
public sealed record BranchDetachRequested(TabId TabId, string CommitHash) : BranchCommand(TabId);

/// <summary>Switch to the selected branch, or open the switch picker when the selection names none.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record BranchSwitchRequested(TabId TabId, BranchTarget Target) : BranchCommand(TabId);

/// <summary>Rename the selected local branch: opens the name prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record BranchRenameRequested(TabId TabId, BranchTarget Target) : BranchCommand(TabId);

/// <summary>Delete the selected local branch: asks for confirmation, or lists the candidates first.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record BranchDeleteRequested(TabId TabId, BranchTarget Target) : BranchCommand(TabId);

/// <summary>Set the upstream of the selected local branch: opens the remote branch picker.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record BranchSetUpstreamRequested(TabId TabId, BranchTarget Target) : BranchCommand(TabId);

/// <summary>Unset the upstream of the selected local branch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record BranchUnsetUpstreamRequested(TabId TabId, BranchTarget Target) : BranchCommand(TabId);

/// <summary>Move the selected local branch to its upstream as last fetched.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record BranchFastForwardRequested(TabId TabId, BranchTarget Target) : BranchCommand(TabId);

/// <summary>The new-branch name prompt was accepted.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="StartPoint">The ref or commit the branch starts at, fixed when the prompt opened.</param>
/// <param name="Label">How the start point is named for the user.</param>
/// <param name="Checkout">True to check the new branch out.</param>
/// <param name="Name">The typed name.</param>
public sealed record BranchNameAccepted(TabId TabId, string StartPoint, string Label, bool Checkout, string Name)
    : BranchCommand(TabId);

/// <summary>The rename prompt was accepted.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="From">The branch the prompt was opened for.</param>
/// <param name="Name">The typed name.</param>
public sealed record BranchRenameAccepted(TabId TabId, string From, string Name) : BranchCommand(TabId);

/// <summary>A candidate was chosen in the switch picker.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Choice">A full ref name for a branch, or the commit hash for the detached entry.</param>
public sealed record BranchSwitchChosen(TabId TabId, string Choice) : BranchCommand(TabId);

/// <summary>A branch was chosen in the delete list.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The chosen local branch.</param>
public sealed record BranchDeleteChosen(TabId TabId, string Name) : BranchCommand(TabId);

/// <summary>A remote branch was chosen in the upstream picker.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branch">The local branch the prompt was opened for.</param>
/// <param name="Upstream">The remote branch, such as <c>origin/topic</c>.</param>
public sealed record BranchUpstreamChosen(TabId TabId, string Branch, string Upstream) : BranchCommand(TabId);

/// <summary><c>y</c> in the delete confirm; the branch is named by what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The branch.</param>
/// <param name="CommitHash">Where it pointed, for the restore hint.</param>
/// <param name="UpstreamFullName">Its configured upstream, null when it has none.</param>
public sealed record BranchDeleteConfirmed(TabId TabId, string Name, string CommitHash, string? UpstreamFullName)
    : BranchCommand(TabId);

/// <summary><c>y</c> in the confirm for a branch that holds commits nothing else reaches.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The branch.</param>
/// <param name="CommitHash">Where it pointed, for the restore hint.</param>
public sealed record BranchForceDeleteConfirmed(TabId TabId, string Name, string CommitHash) : BranchCommand(TabId);

/// <summary>Result: a branch operation finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="Follow">The commit the graph cursor moves to, null to leave it.</param>
public sealed record BranchOperationSucceeded(TabId TabId, string Text, string? Follow)
    : BranchCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Branches;
}

/// <summary>Result: a branch operation failed; the tab is refreshed anyway, git may have done part of it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
public sealed record BranchOperationFailed(TabId TabId, string Text) : BranchCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Branches;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record BranchOperationCancelled(TabId TabId) : BranchCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Branches;
}

/// <summary>Result: the branch holds commits that neither HEAD nor its upstream reach; ask a second time.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The branch.</param>
/// <param name="CommitHash">Where it points.</param>
public sealed record BranchDeleteNeedsForce(TabId TabId, string Name, string CommitHash)
    : BranchCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Branches;
}
