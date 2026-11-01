using System.Collections.Immutable;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Tags;

/// <summary>A tag action or the result of one, for one tab. Dropped when the tab is no longer open (a late
/// result).</summary>
public abstract record TagCommand : AppCommand
{
    private protected TagCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Create a tag at the selected commit: opens the name prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record TagCreateRequested(TabId TabId, TagTarget Target) : TagCommand(TabId);

/// <summary>Delete the selected local tag: asks for confirmation.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record TagDeleteRequested(TabId TabId, TagTarget Target) : TagCommand(TabId);

/// <summary>Push the selected tag: reads the remotes, then opens the remote prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record TagPushRequested(TabId TabId, TagTarget Target) : TagCommand(TabId);

/// <summary>Delete the selected tag on a remote: reads the remotes, then opens the remote prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record TagDeleteOnRemoteRequested(TabId TabId, TagTarget Target) : TagCommand(TabId);

/// <summary>The text of the open prompt was edited.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The whole new text.</param>
public sealed record TagPromptTextChanged(TabId TabId, string Text) : TagCommand(TabId);

/// <summary>The highlight of the remote prompt moved (wraps around).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Step">Rows to move, negative goes up.</param>
public sealed record TagPromptMoved(TabId TabId, int Step) : TagCommand(TabId);

/// <summary>Enter in the open prompt.</summary>
/// <param name="TabId">The tab.</param>
public sealed record TagPromptAccepted(TabId TabId) : TagCommand(TabId);

/// <summary>Esc in the open prompt, or the prompt's tab was left.</summary>
/// <param name="TabId">The tab.</param>
public sealed record TagPromptCancelled(TabId TabId) : TagCommand(TabId);

/// <summary><c>y</c> in the delete confirm; the tag is named by what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The tag.</param>
/// <param name="ObjectHash">The object the tag pointed at, for the undo hint.</param>
public sealed record TagDeleteConfirmed(TabId TabId, string Name, string ObjectHash) : TagCommand(TabId);

/// <summary><c>y</c> in the remote delete confirm.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Remote">The remote.</param>
/// <param name="Name">The tag.</param>
public sealed record TagDeleteOnRemoteConfirmed(TabId TabId, string Remote, string Name) : TagCommand(TabId);

/// <summary>Result: a tag operation finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record TagOperationSucceeded(TabId TabId, string Text) : TagCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Tags;
}

/// <summary>Result: a tag operation failed; the tab is refreshed anyway because git may have done part of it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
/// <param name="Retry">The terminal retry to offer, null when the failure was not about credentials.</param>
public sealed record TagOperationFailed(TabId TabId, string Text, TagRetry? Retry) : TagCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Tags;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record TagOperationCancelled(TabId TabId) : TagCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Tags;
}

/// <summary>Result: the remotes were read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The tag the prompt is for.</param>
/// <param name="Push">True to push, false to delete on the remote.</param>
/// <param name="Remotes">The remote names.</param>
public sealed record TagRemotesLoaded(TabId TabId, string Name, bool Push, ImmutableArray<string> Remotes)
    : TagCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Tags;
}

/// <summary>Result: the remotes could not be read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record TagRemotesFailed(TabId TabId, string Text) : TagCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Tags;
}
