using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Merge;

/// <summary>
/// Merges with the git CLI. <c>--no-edit</c> keeps git from opening an editor for the message. A normal merge goes
/// through <see cref="OperationRunner"/> (so a stop on conflicts is a result); a squash leaves no <c>MERGE_HEAD</c>,
/// so its conflicts are recognised here from the unmerged files.
/// </summary>
public sealed class MergeWriter(GitRunner runner, OperationRunner operations) : IMergeWriter
{
    private const string Merge = "merge";
    private const string NoEdit = "--no-edit";
    private const string NoFastForward = "--no-ff";
    private const string SquashOption = "--squash";

    private static readonly string[] HeadArguments = ["rev-parse", "--quiet", "--verify", GitOptions.HeadRevision];
    private static readonly string[] UnmergedArguments = ["ls-files", "--unmerged"];
    private static readonly string[] ParentsArguments = ["rev-list", "--parents", "-n", "1", GitOptions.HeadRevision];
    private static readonly string[] StagedArguments = ["diff", "--cached", "--quiet"];

    private static readonly GitRunOptions WriteOptions = new(Timeout: TimeSpan.FromMinutes(10));

    private static readonly GitRunOptions NoMatchOptions = new(
        ReadOnly: true,
        AcceptedExitCodes: [GitRunOptions.NoMatchExitCode]);

    public async Task<MergeResult> MergeAsync(
        RepositoryLocation location,
        string target,
        MergeMode mode,
        CancellationToken cancellationToken)
    {
        // A cancel before the start must not run a write that a short-lived process would finish before it is noticed.
        cancellationToken.ThrowIfCancellationRequested();
        var before = await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false);
        string[] arguments = mode switch
        {
            MergeMode.NoFastForward => [Merge, NoEdit, NoFastForward, target, GitOptions.EndOfOptions],
            MergeMode.Squash => [Merge, NoEdit, SquashOption, target, GitOptions.EndOfOptions],
            _ => [Merge, NoEdit, target, GitOptions.EndOfOptions],
        };

        return mode == MergeMode.Squash
            ? await SquashAsync(location, arguments, before, cancellationToken).ConfigureAwait(false)
            : await MergeOperationAsync(location, arguments, before, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MergeResult> MergeOperationAsync(
        RepositoryLocation location,
        string[] arguments,
        string? before,
        CancellationToken cancellationToken)
    {
        var result = await operations.RunAsync(location, arguments, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new MergeResult(MergeKind.Conflicted, result.Head, before);
        }

        if (result.Head == before)
        {
            return new MergeResult(MergeKind.UpToDate, result.Head, before);
        }

        var parents = await runner
            .RunAsync(location.CommandPath, ParentsArguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);
        var isMergeCommit = parents.StandardOutputText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 2;
        return new MergeResult(isMergeCommit ? MergeKind.MergeCommit : MergeKind.FastForward, result.Head, before);
    }

    private async Task<MergeResult> SquashAsync(
        RepositoryLocation location,
        string[] arguments,
        string? before,
        CancellationToken cancellationToken)
    {
        var unmergedBefore = await ReadUnmergedAsync(location, cancellationToken).ConfigureAwait(false);
        try
        {
            await runner.RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GitCommandFailedException)
        {
            var unmergedAfter = await ReadUnmergedAsync(location, cancellationToken).ConfigureAwait(false);
            if (unmergedAfter.Length == 0 || unmergedAfter == unmergedBefore)
            {
                throw;
            }

            return new MergeResult(MergeKind.Conflicted, before, before);
        }

        var staged = await runner
            .RunAsync(location.CommandPath, StagedArguments, NoMatchOptions, cancellationToken)
            .ConfigureAwait(false);
        return new MergeResult(staged.ExitCode == 0 ? MergeKind.UpToDate : MergeKind.Squashed, before, before);
    }

    private async Task<string?> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, HeadArguments, NoMatchOptions, cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutputText.Trim() : null;
    }

    private async Task<string> ReadUnmergedAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, UnmergedArguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);
        return result.StandardOutputText;
    }
}
