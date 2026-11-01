using System.Collections.Immutable;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Remotes;

/// <summary>A remote management action or the result of one, for one tab. Dropped when the tab is no longer open (a
/// late result or answer).</summary>
public abstract record RemoteCommand : AppCommand
{
    private protected RemoteCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Add, remove, rename or re-point a remote: reads the remotes first.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Action">What was asked for.</param>
/// <param name="Target">What is selected.</param>
public sealed record RemoteActionRequested(TabId TabId, RemoteAction Action, RemoteTarget Target) : RemoteCommand(TabId);

/// <summary>A remote was chosen in the picker.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Action">What was asked for.</param>
/// <param name="Remotes">The remotes read when the action started.</param>
/// <param name="Name">The chosen remote.</param>
public sealed record RemoteChosen(TabId TabId, RemoteAction Action, ImmutableArray<RemoteInfo> Remotes, string Name)
    : RemoteCommand(TabId);

/// <summary>The name prompt of an added remote was accepted: check the name and ask for the URL.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Remotes">The remotes read when the action started.</param>
/// <param name="Name">The typed name.</param>
public sealed record RemoteAddNameAccepted(TabId TabId, ImmutableArray<RemoteInfo> Remotes, string Name)
    : RemoteCommand(TabId);

/// <summary>The URL prompt of an added remote was accepted: add it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The accepted name of the new remote.</param>
/// <param name="Url">The typed URL.</param>
public sealed record RemoteAddUrlAccepted(TabId TabId, string Name, string Url) : RemoteCommand(TabId);

/// <summary>The new-name prompt of a rename was accepted.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Remotes">The remotes read when the action started.</param>
/// <param name="OldName">The remote being renamed.</param>
/// <param name="NewName">The typed name.</param>
public sealed record RemoteRenameAccepted(
    TabId TabId,
    ImmutableArray<RemoteInfo> Remotes,
    string OldName,
    string NewName) : RemoteCommand(TabId);

/// <summary>The URL prompt of a set-URL was accepted.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The remote.</param>
/// <param name="OldUrl">Its URL when the prompt opened.</param>
/// <param name="Url">The typed URL.</param>
public sealed record RemoteUrlAccepted(TabId TabId, string Name, string OldUrl, string Url) : RemoteCommand(TabId);

/// <summary><c>y</c> in the remove confirm; the remote is named by what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The remote.</param>
/// <param name="Url">Its URL, for the restore hint.</param>
public sealed record RemoteRemoveConfirmed(TabId TabId, string Name, string Url) : RemoteCommand(TabId);

/// <summary>Result: the remotes were read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Action">What was asked for.</param>
/// <param name="Target">What was selected.</param>
/// <param name="Remotes">The configured remotes.</param>
public sealed record RemotesListed(
    TabId TabId,
    RemoteAction Action,
    RemoteTarget Target,
    ImmutableArray<RemoteInfo> Remotes) : RemoteCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Remotes;
}

/// <summary>Result: the remotes could not be read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record RemotesListFailed(TabId TabId, string Text) : RemoteCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Remotes;
}

/// <summary>Result: a remote operation finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record RemoteOperationSucceeded(TabId TabId, string Text) : RemoteCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Remotes;
}

/// <summary>Result: a remote operation failed; the tab is refreshed anyway because git may have done part of it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
public sealed record RemoteOperationFailed(TabId TabId, string Text) : RemoteCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Remotes;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record RemoteOperationCancelled(TabId TabId) : RemoteCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Remotes;
}
