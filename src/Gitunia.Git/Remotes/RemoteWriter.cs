using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Remotes;

/// <summary>Fetch, pull and push with the git CLI through <see cref="RemoteRunner"/>.</summary>
public sealed class RemoteWriter(RemoteRunner remoteRunner) : IRemoteWriter
{
    private const string Fetch = "fetch";
    private const string Merge = "merge";
    private const string UpstreamRevision = "@{upstream}";
    private const string FastForwardOnly = "--ff-only";
    private const string UpToDateText = "Already up to date";
    private const string CannotFastForwardText = "Not possible to fast-forward";

    public async Task FetchAsync(
        RepositoryLocation location,
        FetchRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        var arguments = RemoteCommands.BuildFetch(request, true);
        await remoteRunner.RunAsync(location, arguments, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PullOutcome> PullAsync(
        RepositoryLocation location,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Fetch (cancellable, leaves only complete objects and updated refs) then a local fast-forward merge. The merge
        // rewrites the index and working tree, so it is never killed halfway: a cancel is honoured between the steps.
        await remoteRunner
            .RunAsync(location, [Fetch, RemoteRunner.ProgressOption], progress, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var result = await remoteRunner
                .RunAsync(location, [Merge, FastForwardOnly, UpstreamRevision], null, CancellationToken.None)
                .ConfigureAwait(false);
            if (result.StandardOutputText.Contains(UpToDateText, StringComparison.Ordinal))
            {
                return PullOutcome.UpToDate;
            }

            return PullOutcome.FastForwarded;
        }
        catch (GitCommandFailedException exception)
            when (exception.StandardError.Contains(CannotFastForwardText, StringComparison.Ordinal))
        {
            throw new PullDivergedException(CannotFastForwardText);
        }
    }

    public async Task PushAsync(
        RepositoryLocation location,
        PushRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        var arguments = RemoteCommands.BuildPush(request, true);
        await remoteRunner.RunAsync(location, arguments, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRemoteBranchAsync(
        RepositoryLocation location,
        string remote,
        string branch,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        // The lease makes git refuse the delete when the branch moved since the tip the user confirmed.
        var arguments = RemoteCommands.BuildDeleteBranch(remote, branch, expectedHash, true);
        await remoteRunner.RunAsync(location, arguments, null, cancellationToken).ConfigureAwait(false);
    }
}
