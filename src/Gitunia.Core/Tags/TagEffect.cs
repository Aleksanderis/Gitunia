using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Tags;

/// <summary>An operation on tags. Each posts one result <see cref="TagCommand"/>; none changes state itself.</summary>
public abstract record TagEffect : AppEffect
{
    private protected TagEffect(TabId tabId, RepositoryLocation location)
    {
        TabId = tabId;
        Location = location;
    }

    /// <summary>The tab the result belongs to.</summary>
    public TabId TabId { get; }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }
}

/// <summary>Create a tag: lightweight with an empty message, annotated otherwise.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The new tag.</param>
/// <param name="StartPoint">The commit it points at.</param>
/// <param name="Message">The annotation, empty for none.</param>
/// <param name="Label">How the start point is named for the user.</param>
public sealed record CreateTagEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Name,
    string StartPoint,
    string Message,
    string Label) : TagEffect(TabId, Location);

/// <summary>Delete a local tag.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The tag.</param>
/// <param name="ObjectHash">What it pointed at, for the restore hint.</param>
public sealed record DeleteTagEffect(TabId TabId, RepositoryLocation Location, string Name, string ObjectHash)
    : TagEffect(TabId, Location);

/// <summary>Read the remote names for the remote prompt of a push or a remote delete.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The tag the prompt is for.</param>
/// <param name="Push">True to push, false to delete on the remote.</param>
public sealed record ListTagRemotesEffect(TabId TabId, RepositoryLocation Location, string Name, bool Push)
    : TagEffect(TabId, Location);

/// <summary>Push one tag to a remote.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Remote">The remote.</param>
/// <param name="Name">The tag.</param>
public sealed record PushTagEffect(TabId TabId, RepositoryLocation Location, string Remote, string Name)
    : TagEffect(TabId, Location);

/// <summary>Delete one tag on a remote; the local tag stays.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Remote">The remote.</param>
/// <param name="Name">The tag.</param>
public sealed record DeleteTagOnRemoteEffect(TabId TabId, RepositoryLocation Location, string Remote, string Name)
    : TagEffect(TabId, Location);
