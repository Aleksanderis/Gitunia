using Gitunia.Core.Lists;
using Gitunia.Core.Running;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Discard;

/// <summary>A discard key, its answer or the result of one, for one tab. Dropped when the tab is no longer open (a late
/// result, or an answer given after the tab was closed).</summary>
public abstract record DiscardCommand : AppCommand
{
    private protected DiscardCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>The discard key in the Unstaged list: the file or folder under the cursor, or the Visual range.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Range">The Visual range of rows of the list, null for the row under the cursor.</param>
public sealed record DiscardRequested(TabId TabId, RowRange? Range = null) : DiscardCommand(TabId);

/// <summary>The discard key in the Unstaged diff: the hunk or Visual lines the terminal layer read from the shown
/// diff.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Selection">What the user picked.</param>
/// <param name="Move">Where the diff cursor goes afterwards.</param>
public sealed record DiscardSelectionRequested(TabId TabId, StagingSelection Selection, DiffCursorMove Move)
    : DiscardCommand(TabId);

/// <summary><c>y</c> in the discard dialog. It carries exactly what the user saw named.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is thrown away.</param>
/// <param name="EndsRange">The target came from a Visual range, which ends once the discard starts.</param>
public sealed record DiscardConfirmed(TabId TabId, DiscardTarget Target, bool EndsRange) : DiscardCommand(TabId);

/// <summary>Result: the discard ended and the status of the tab has been read again.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Move">Where the diff cursor goes, null when nothing was discarded or the key was not in the diff.</param>
public sealed record DiscardSucceeded(TabId TabId, string Text, StatusSeverity Severity, DiffCursorMove? Move)
    : DiscardCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Discard;
}

/// <summary>Result: git failed; the status was read again anyway because part of the change may be done.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
public sealed record DiscardFailed(TabId TabId, string Text) : DiscardCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Discard;
}

/// <summary>Result: the discard was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record DiscardCancelled(TabId TabId) : DiscardCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Discard;
}
