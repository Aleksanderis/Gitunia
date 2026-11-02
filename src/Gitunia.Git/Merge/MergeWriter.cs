using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs;
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
    private const string CommitSuffix = "^{commit}";

    private static readonly string[] HeadArguments = ["rev-parse", "--quiet", "--verify", GitOptions.HeadRevision];

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
        var chosen = await ChooseRevisionAsync(location, target, cancellationToken).ConfigureAwait(false);
        var revision = chosen.Revision;
        var message = chosen.Message;
        string[] messageArguments = message is null ? [] : ["-m", message];
        string[] arguments = mode switch
        {
            MergeMode.NoFastForward =>
                [Merge, NoEdit, NoFastForward, .. messageArguments, revision, GitOptions.EndOfOptions],
            MergeMode.Squash => [Merge, NoEdit, SquashOption, revision, GitOptions.EndOfOptions],
            _ => [Merge, NoEdit, .. messageArguments, revision, GitOptions.EndOfOptions],
        };

        if (mode == MergeMode.Squash)
        {
            return await SquashAsync(location, arguments, revision, before, cancellationToken).ConfigureAwait(false);
        }

        var resolved = await runner
            .RunAsync(
                location.CommandPath,
                ["rev-parse", "--quiet", "--verify", $"{revision}{CommitSuffix}"],
                NoMatchOptions,
                cancellationToken)
            .ConfigureAwait(false);
        var resolvedTarget = resolved.ExitCode == 0 ? resolved.StandardOutputText.Trim() : null;
        return await MergeOperationAsync(location, arguments, before, resolvedTarget, mode, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A full branch ref is only needed when the short name would resolve to something else (a tag of the same
    /// name): then it is passed with <c>-m</c>, because git would write the full ref into the message. Otherwise
    /// the short name goes through and git writes its own message (<c>into &lt;branch&gt;</c>, merge.log and
    /// merge.suppressDest included). A hash or any other revision is passed as is.
    /// </summary>
    private async Task<MergeRevision> ChooseRevisionAsync(
        RepositoryLocation location,
        string target,
        CancellationToken cancellationToken)
    {
        string? label;
        string shortName;
        if (target.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal))
        {
            shortName = target[GitRefPrefixes.Heads.Length..];
            label = $"Merge branch '{shortName}'";
        }
        else if (target.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal))
        {
            shortName = target[GitRefPrefixes.Remotes.Length..];
            label = $"Merge remote-tracking branch '{shortName}'";
        }
        else
        {
            return new MergeRevision(target, null);
        }

        var full = await ResolveCommitAsync(location, target, cancellationToken).ConfigureAwait(false);
        var named = await ResolveCommitAsync(location, shortName, cancellationToken).ConfigureAwait(false);
        return full is not null && full == named ? new MergeRevision(shortName, null) : new MergeRevision(target, label);
    }

    private async Task<string?> ResolveCommitAsync(
        RepositoryLocation location,
        string revision,
        CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(
                location.CommandPath,
                ["rev-parse", "--quiet", "--verify", $"{revision}{CommitSuffix}"],
                NoMatchOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutputText.Trim() : null;
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
        var unmergedBefore = await UnmergedFiles.ReadAsync(runner, location, cancellationToken).ConfigureAwait(false);
        try
        {
            await runner.RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GitCommandFailedException)
        {
            var unmergedAfter = await UnmergedFiles.ReadAsync(runner, location, cancellationToken).ConfigureAwait(false);
            if (!UnmergedFiles.MadeConflicts(unmergedBefore, unmergedAfter))
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
}
