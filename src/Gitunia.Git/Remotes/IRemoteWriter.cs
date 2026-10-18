using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Remotes;

/// <summary>
/// Fetch, pull and push of the repository's current branch with streamed progress. Every call is cancellable
/// (a cancel kills git; the repository stays consistent because git updates refs only after the objects arrived),
/// has an idle timeout, and never prompts: credentials come from the user's helpers, and a call that needs input
/// fails with git's message (#45 offers the real terminal). Failures throw the typed git exceptions.
/// </summary>
public interface IRemoteWriter
{
    /// <summary>Fetches the remote (or all remotes), optionally pruning.</summary>
    Task FetchAsync(
        RepositoryLocation location,
        FetchRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Pulls the current branch: a cancellable fetch, then <c>merge --ff-only @{upstream}</c>, so it never creates a
    /// merge commit or rebases. A cancel is honoured between the steps; the merge itself is not interrupted.
    /// </summary>
    /// <exception cref="PullDivergedException">The branches diverged; nothing was changed.</exception>
    Task<PullOutcome> PullAsync(
        RepositoryLocation location,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Pushes the current branch the way git's configuration says, or what the request names. It forces only with a
    /// <see cref="PushLease"/>, never plainly.
    /// </summary>
    Task PushAsync(
        RepositoryLocation location,
        PushRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>Deletes <c>refs/heads/&lt;branch&gt;</c> on <paramref name="remote"/>; local refs stay.</summary>
    Task DeleteRemoteBranchAsync(
        RepositoryLocation location,
        string remote,
        string branch,
        CancellationToken cancellationToken);
}
