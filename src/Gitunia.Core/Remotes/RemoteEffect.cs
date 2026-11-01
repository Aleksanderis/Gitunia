using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Remotes;

/// <summary>An operation on remotes. Each posts one result <see cref="RemoteCommand"/>; none changes state itself.</summary>
public abstract record RemoteEffect : OperationEffect
{
    private protected RemoteEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Remotes)
    {
        Location = location;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }
}

/// <summary>Read the configured remotes before the first prompt of an action.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Action">What was asked for.</param>
/// <param name="Target">What was selected.</param>
public sealed record ListRemotesEffect(
    TabId TabId,
    RepositoryLocation Location,
    RemoteAction Action,
    RemoteTarget Target) : RemoteEffect(TabId, Location);

/// <summary>Add a remote; nothing is fetched.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The new remote.</param>
/// <param name="Url">Its URL.</param>
public sealed record AddRemoteEffect(TabId TabId, RepositoryLocation Location, string Name, string Url)
    : RemoteEffect(TabId, Location);

/// <summary>Remove a remote; git drops its remote-tracking branches.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The remote.</param>
/// <param name="Url">Its URL, for the restore hint.</param>
public sealed record RemoveRemoteEffect(TabId TabId, RepositoryLocation Location, string Name, string Url)
    : RemoteEffect(TabId, Location);

/// <summary>Rename a remote; git moves its remote-tracking branches along.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="OldName">The remote.</param>
/// <param name="NewName">Its new name.</param>
public sealed record RenameRemoteEffect(TabId TabId, RepositoryLocation Location, string OldName, string NewName)
    : RemoteEffect(TabId, Location);

/// <summary>Change the URL of a remote.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The remote.</param>
/// <param name="OldUrl">Its URL before, for the message.</param>
/// <param name="Url">The new URL.</param>
public sealed record SetRemoteUrlEffect(TabId TabId, RepositoryLocation Location, string Name, string OldUrl, string Url)
    : RemoteEffect(TabId, Location);
