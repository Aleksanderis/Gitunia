using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Merge;

/// <summary>
/// Merges with the git CLI. <c>--no-edit</c> keeps git from opening an editor for the message. A normal merge goes
/// through <see cref="OperationRunner"/> (so a stop on conflicts is a result); a squash leaves no <c>MERGE_HEAD</c>,
/// so its conflicts are recognised here from the unmerged files. The end-of-options marker follows the target
/// because <c>merge</c> does not accept it before the revision; targets are branch names or hashes from the UI.
/// </summary>
public sealed class MergeWriter(GitRunner runner, OperationRunner operations) : IMergeWriter
{
    private const string Merge = "merge";
    private const string NoEdit = "--no-edit";
    private const string NoFastForward = "--no-ff";
    private const string SquashOption = "--squash";
    private const string MergeBase = "merge-base";
    private const string IsAncestor = "--is-ancestor";
    private const string LocalPrefix = "refs/heads/";
    private const string RemotePrefix = "refs/remotes/";
    private const string CommitSuffix = "^{commit}";

    private static readonly string[] HeadArguments = ["rev-parse", "--quiet", "--verify", GitOptions.HeadRevision];
    private static readonly string[] UnmergedArguments = ["ls-files", "--unmerged"];

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
        var message = MessageFor(target);
        string[] messageArguments = message is null ? [] : ["-m", message];
        string[] arguments = mode switch
        {
            MergeMode.NoFastForward =>
                [Merge, NoEdit, NoFastForward, .. messageArguments, target, GitOptions.EndOfOptions],
            MergeMode.Squash => [Merge, NoEdit, SquashOption, target, GitOptions.EndOfOptions],
            _ => [Merge, NoEdit, .. messageArguments, target, GitOptions.EndOfOptions],
        };

        if (mode == MergeMode.Squash)
        {
            return await SquashAsync(location, arguments, target, before, cancellationToken).ConfigureAwait(false);
        }

        var resolved = await runner
            .RunAsync(
                location.CommandPath,
                ["rev-parse", "--quiet", "--verify", $"{target}{CommitSuffix}"],
                NoMatchOptions,
                cancellationToken)
            .ConfigureAwait(false);
        var resolvedTarget = resolved.ExitCode == 0 ? resolved.StandardOutputText.Trim() : null;
        return await MergeOperationAsync(location, arguments, before, resolvedTarget, mode, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Git names a full ref in its message (<c>Merge branch 'refs/heads/x'</c>); the text git writes for the short
    /// name is given instead. Null for a hash or any other revision, which keeps git's own message.
    /// </summary>
    private static string? MessageFor(string target)
    {
        if (target.StartsWith(LocalPrefix, StringComparison.Ordinal) && target.Length > LocalPrefix.Length)
        {
            return $"Merge branch '{target[LocalPrefix.Length..]}'";
        }

        return target.StartsWith(RemotePrefix, StringComparison.Ordinal) && target.Length > RemotePrefix.Length
            ? $"Merge remote-tracking branch '{target[RemotePrefix.Length..]}'"
            : null;
    }

    private async Task<MergeResult> MergeOperationAsync(
        RepositoryLocation location,
        string[] arguments,
        string? before,
        string? resolvedTarget,
        MergeMode mode,
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

        // A fast-forward leaves HEAD exactly on the target, even when the target is itself a merge commit.
        var isFastForward = mode == MergeMode.Default && result.Head == resolvedTarget;
        return new MergeResult(isFastForward ? MergeKind.FastForward : MergeKind.MergeCommit, result.Head, before);
    }

    private async Task<MergeResult> SquashAsync(
        RepositoryLocation location,
        string[] arguments,
        string target,
        string? before,
        CancellationToken cancellationToken)
    {
        // Decided before the call: what is staged already (the user's own work) must not read as a squash result.
        var contained = await runner
            .RunAsync(
                location.CommandPath,
                [MergeBase, IsAncestor, $"{target}{CommitSuffix}", GitOptions.HeadRevision],
                NoMatchOptions,
                cancellationToken)
            .ConfigureAwait(false);
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

        return new MergeResult(contained.ExitCode == 0 ? MergeKind.UpToDate : MergeKind.Squashed, before, before);
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
