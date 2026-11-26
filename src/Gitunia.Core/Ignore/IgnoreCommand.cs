using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Ignore;

/// <summary>An ignore key, its answer or the result of one, for one tab. Dropped when the tab is no longer open.</summary>
public abstract record IgnoreCommand : AppCommand
{
    private protected IgnoreCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>The ignore key in the Unstaged list or its diff: the untracked file or folder under the cursor.</summary>
/// <param name="TabId">The tab.</param>
public sealed record IgnoreRequested(TabId TabId) : IgnoreCommand(TabId);

/// <summary>The answer of the pattern prompt: the pattern of the option the user picked, fixed when it opened.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Pattern">The line to add to <c>.gitignore</c>.</param>
public sealed record IgnoreChosen(TabId TabId, string Pattern) : IgnoreCommand(TabId);

/// <summary>Result: the pattern is in <c>.gitignore</c> and the status of the tab has been read again.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How serious it is.</param>
public sealed record IgnoreSucceeded(TabId TabId, string Text, StatusSeverity Severity)
    : IgnoreCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Ignore;
}

/// <summary>Result: the file could not be read or written.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The reason with what was tried.</param>
public sealed record IgnoreFailed(TabId TabId, string Text) : IgnoreCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Ignore;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record IgnoreCancelled(TabId TabId) : IgnoreCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Ignore;
}
