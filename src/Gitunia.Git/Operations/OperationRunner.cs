using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Operations;

/// <summary>
/// Runs one git call that starts or advances a multi-step operation and turns a stop on conflicts into a result
/// (<see cref="OperationOutcome.Conflicted"/>) instead of a failure. The one place that decides "completed or
/// conflicted", so merge, rebase, cherry-pick, revert and their continue, abort and skip all report the same way.
/// A call that fails without leaving unmerged files behind (a dirty tree, a refused call) still throws.
/// </summary>
public sealed class OperationRunner(GitRunner runner, IRepositoryOperationDetector detector)
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(10);

    private static readonly GitRunOptions WriteOptions = new(Timeout: OperationTimeout);


    private static readonly string[] HeadArguments = ["rev-parse", "--quiet", "--verify", GitOptions.HeadRevision];

    private static readonly GitRunOptions HeadOptions = new(
        ReadOnly: true,
        AcceptedExitCodes: [GitRunOptions.NoMatchExitCode]);

    /// <summary>
    /// Runs <paramref name="arguments"/> in the worktree. Callers whose command could open an editor put
    /// <c>-c core.editor=true</c> (or <c>--no-edit</c>) in the arguments themselves; this class never adds options.
    /// </summary>
    public async Task<OperationResult> RunAsync(
        RepositoryLocation location,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var unmergedBefore = await UnmergedFiles.ReadAsync(runner, location, cancellationToken).ConfigureAwait(false);
        try
        {
            await runner.RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GitCommandFailedException)
        {
            // Only a change of the unmerged set proves this call stopped on conflicts; files that were already
            // conflicted (a continue with nothing resolved) mean the call was refused, and the message is the answer.
            if (!await StoppedOnConflictsAsync(location, unmergedBefore, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }

            var stopped = await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false);
            return new OperationResult(OperationOutcome.Conflicted, stopped);
        }

        var head = await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false);
        return new OperationResult(OperationOutcome.Completed, head);
    }

    /// <summary>
    /// Runs a call that must end the operation (an abort): any failure throws, because stopping on conflicts is not
    /// a possible outcome of it.
    /// </summary>
    public async Task<OperationResult> RunToCompletionAsync(
        RepositoryLocation location,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        await runner.RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken).ConfigureAwait(false);
        var head = await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false);
        return new OperationResult(OperationOutcome.Completed, head);
    }

    private async Task<bool> StoppedOnConflictsAsync(
        RepositoryLocation location,
        string unmergedBefore,
        CancellationToken cancellationToken)
    {
        if (detector.Detect(location) == RepositoryOperation.None)
        {
            return false;
        }

        var unmergedAfter = await UnmergedFiles.ReadAsync(runner, location, cancellationToken).ConfigureAwait(false);
        return UnmergedFiles.MadeConflicts(unmergedBefore, unmergedAfter);
    }

    private async Task<string?> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, HeadArguments, HeadOptions, cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutputText.Trim() : null;
    }
}
