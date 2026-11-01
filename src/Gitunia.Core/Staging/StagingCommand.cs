using Gitunia.Core.Lists;
using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Staging;

/// <summary>A staging action or the result of one, for one tab. Dropped when the tab is no longer open (a late
/// result or an answer).</summary>
public abstract record StagingCommand : AppCommand
{
    private protected StagingCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>A staging key of the file lists: stage or unstage the file under the cursor, the Visual range, or
/// everything.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Action">The key.</param>
/// <param name="Range">The Visual range of rows of the focused list, null for the row under the cursor.</param>
public sealed record StagingRequested(TabId TabId, StagingAction Action, RowRange? Range = null)
    : StagingCommand(TabId);

/// <summary>Stage every unstaged file whatever the focus (untracked included, conflicted skipped) and reduce
/// <paramref name="Then"/> once the status shows the result; it does not run after a failure.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Then">The command that follows (the commit that asked to stage all first).</param>
public sealed record StageAllThenRequested(TabId TabId, AppCommand Then) : StagingCommand(TabId);

/// <summary>Stage the hunk or lines picked in the Changes view diff.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Selection">What the user picked.</param>
/// <param name="Move">Where the diff cursor goes afterwards.</param>
public sealed record StageSelectionRequested(TabId TabId, StagingSelection Selection, DiffCursorMove Move)
    : StagingCommand(TabId);

/// <summary>The conflict editor saved a conflicted file with no marker left: stage it without a dialog.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The path of the file.</param>
public sealed record StageResolvedRequested(TabId TabId, string Path) : StagingCommand(TabId);

/// <summary><c>y</c> in the mark-resolved dialog; the file is the one the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Entry">The conflicted file.</param>
public sealed record ResolveConfirmed(TabId TabId, StatusEntry Entry) : StagingCommand(TabId);

/// <summary>Result: the index was changed and the status of the tab has been read again.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Follow">What happens next; <see cref="StagingFollow.None"/> when the change was not made.</param>
public sealed record StagingSucceeded(TabId TabId, string Text, StatusSeverity Severity, StagingFollow Follow)
    : StagingCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Staging;
}

/// <summary>Result: git failed; the status was read again anyway because git may have changed part of the
/// index.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
public sealed record StagingFailed(TabId TabId, string Text) : StagingCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Staging;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record StagingCancelled(TabId TabId) : StagingCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Staging;
}
