using Gitunia.Core.Status;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Rebase;

/// <summary>An operation of the interactive rebase editor. Each posts one result command.</summary>
public abstract record RebaseEditorEffect : OperationEffect
{
    private protected RebaseEditorEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Rebase)
    {
        Location = location;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }
}

/// <summary>Read the commits a rebase onto the target would replay, to fill the todo.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Generation">The read number; the result is dropped when the session moved on.</param>
/// <param name="TargetHash">The commit to rebase onto, null for the whole history.</param>
/// <param name="Label">How the target is named in messages.</param>
/// <param name="BranchName">The current branch that is rebased.</param>
/// <param name="OldTip">The branch tip when the range was asked for.</param>
public sealed record ReadRebaseRangeEffect(
    TabId TabId,
    RepositoryLocation Location,
    long Generation,
    string? TargetHash,
    string Label,
    string BranchName,
    string OldTip) : RebaseEditorEffect(TabId, Location)
{
    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new RebaseEditorReadFailed(TabId, text, StatusSeverity.Error);
}

/// <summary>Read the full message of a commit that is reworded.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Generation">The read number; the result is dropped when the session moved on.</param>
/// <param name="Hash">The full hash as the todo row has it.</param>
/// <param name="Id">The parsed hash.</param>
/// <param name="Subject">The subject, which the editor starts with when git cannot deliver the message.</param>
public sealed record ReadRebaseMessageEffect(
    TabId TabId,
    RepositoryLocation Location,
    long Generation,
    string Hash,
    ObjectId Id,
    string Subject) : RebaseEditorEffect(TabId, Location)
{
    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new RebaseEditorReadFailed(TabId, text, StatusSeverity.Error);
}

/// <summary>Hand the todo to the engine and run the rebase.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="State">The todo as the user confirmed it.</param>
public sealed record RunInteractiveRebaseEffect(TabId TabId, RepositoryLocation Location, RebaseEditorState State)
    : RebaseEditorEffect(TabId, Location)
{
    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new RebaseFailed(TabId, text);
}
