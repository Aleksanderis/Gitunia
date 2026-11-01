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

/// <summary>The name prompt was accepted: check the name and ask for the message.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="StartPoint">The commit the tag is made at, fixed when the prompt opened.</param>
/// <param name="Label">How the start point is named for the user.</param>
/// <param name="Name">The typed name.</param>
public sealed record TagNameAccepted(TabId TabId, string StartPoint, string Label, string Name) : TagCommand(TabId);

/// <summary>The message prompt was accepted: create the tag (lightweight when the message is empty).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="StartPoint">The commit the tag is made at.</param>
/// <param name="Label">How the start point is named for the user.</param>
/// <param name="Name">The accepted name of the new tag.</param>
/// <param name="Message">The typed message.</param>
public sealed record TagMessageAccepted(TabId TabId, string StartPoint, string Label, string Name, string Message)
    : TagCommand(TabId);

/// <summary>A remote was chosen in the picker: push the tag to it, or ask before deleting the tag on it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="TagName">The tag the prompt was opened for.</param>
/// <param name="Push">True to push, false to delete on the remote.</param>
/// <param name="Remote">The chosen remote.</param>
public sealed record TagRemoteChosen(TabId TabId, string TagName, bool Push, string Remote) : TagCommand(TabId);

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
