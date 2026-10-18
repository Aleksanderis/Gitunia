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
    private const string Push = "push";
    private const string SetUpstreamOption = "--set-upstream";
    private const string LeaseOption = "--force-with-lease";
    private const string DeleteOption = "--delete";
    private const string NoProgressOption = "--no-progress";
    private const string Heads = "refs/heads/";
    private const string AllOption = "--all";
    private const string PruneOption = "--prune";
    private const string FastForwardOnly = "--ff-only";
    private const string UpToDateText = "Already up to date";
    private const string CannotFastForwardText = "Not possible to fast-forward";

    public async Task FetchAsync(
        RepositoryLocation location,
        FetchRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string> { Fetch, RemoteRunner.ProgressOption };
        if (request.Prune)
        {
            arguments.Add(PruneOption);
        }

        if (request.AllRemotes)
        {
            arguments.Add(AllOption);
        }
        else if (request.Remote is not null)
        {
            arguments.Add(GitOptions.EndOfOptions);
            arguments.Add(request.Remote);
        }

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
            return result.StandardOutputText.Contains(UpToDateText, StringComparison.Ordinal)
                ? PullOutcome.UpToDate
                : PullOutcome.FastForwarded;
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
        var arguments = new List<string> { Push, RemoteRunner.ProgressOption };
        if (request.SetUpstream)
        {
            arguments.Add(SetUpstreamOption);
        }

        if (request.Lease is { } lease)
        {
            arguments.Add($"{LeaseOption}={Heads}{lease.RemoteBranch}:{lease.ExpectedHash}");
        }

        if (request.Remote is not null)
        {
            arguments.Add(GitOptions.EndOfOptions);
            arguments.Add(request.Remote);
            if (request.Branch is not null)
            {
                arguments.Add(
                    request.Lease is { } target ? $"{request.Branch}:{Heads}{target.RemoteBranch}" : request.Branch);
            }
        }

        await remoteRunner.RunAsync(location, arguments, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRemoteBranchAsync(
        RepositoryLocation location,
        string remote,
        string branch,
        CancellationToken cancellationToken)
    {
        await remoteRunner
            .RunAsync(
                location,
                [Push, NoProgressOption, DeleteOption, GitOptions.EndOfOptions, remote, Heads + branch],
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
