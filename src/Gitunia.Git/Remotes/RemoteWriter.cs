using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Remotes;

/// <summary>Fetch, pull and push with the git CLI through <see cref="RemoteRunner"/>.</summary>
public sealed class RemoteWriter(RemoteRunner remoteRunner) : IRemoteWriter
{
    private const string Fetch = "fetch";
    private const string Pull = "pull";
    private const string Push = "push";
    private const string AllOption = "--all";
    private const string PruneOption = "--prune";
    private const string FastForwardOnly = "--ff-only";
    private const string NoRebase = "--no-rebase";
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
        try
        {
            var result = await remoteRunner
                .RunAsync(
                    location,
                    [Pull, RemoteRunner.ProgressOption, NoRebase, FastForwardOnly],
                    progress,
                    cancellationToken)
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
        if (request.Remote is not null)
        {
            arguments.Add(GitOptions.EndOfOptions);
            arguments.Add(request.Remote);
        }

        await remoteRunner.RunAsync(location, arguments, progress, cancellationToken).ConfigureAwait(false);
    }
}
