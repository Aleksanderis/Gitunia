using System.Globalization;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Pick;

/// <summary>
/// Cherry-picks and reverts with the git CLI through <see cref="OperationRunner"/>, so a stop on conflicts is a result.
/// <c>--no-edit</c> keeps git from opening an editor for the message. A cherry-pick passes <c>--empty=drop</c>: a
/// commit whose changes HEAD has already is skipped instead of stopping the sequence for a decision (when all of them
/// are, the result is <see cref="PickOutcome.NothingToApply"/>); a git too old to know the option (usage error, before
/// anything ran) gets the call again without it, and an empty pick then stops like git's default. The commits are
/// hashes from the UI; the end-of-options marker follows them. A call that fails without a conflict stop but leaves
/// its own operation behind (a range whose later commit turns empty or is refused) is aborted, so a git failure never
/// leaves a pick or revert in progress. A cancel or timeout midway of a multi-commit sequence is rolled back the same
/// way (the earlier commits are undone, the old tip stays in the reflog); an operation that was already in progress is
/// never touched.
/// </summary>
/// <param name="runner">Runs the read-only calls.</param>
/// <param name="operations">Runs the pick and revert calls.</param>
/// <param name="detector">Tells whether an operation is in progress.</param>
/// <param name="dropEmptyOption">Skips empty picks; tests pass an unknown one to force the old-git path.</param>
public sealed class PickWriter(
    GitRunner runner,
    OperationRunner operations,
    IRepositoryOperationDetector detector,
    string dropEmptyOption = PickWriter.DropEmpty) : IPickWriter
{
    /// <summary>The option that makes a cherry-pick skip commits HEAD already has (git 2.45 and newer).</summary>
    public const string DropEmpty = "--empty=drop";

    private const string NoEdit = "--no-edit";
    private const string AbortOption = "--abort";
    private const string ResetCommand = "reset";
    private const string MergeOption = "--merge";
    private const string MainlineOption = "-m";
    private const int UsageExitCode = 129;

    /// <summary>
    /// The most commits one call takes: each adds a 40 character hash to the command line, which Windows caps at
    /// 32,767 characters (about 790 commits), so longer lists are refused with a clear message.
    /// </summary>
    public const int MaxCommits = 500;

    private static readonly string[] HeadArguments = ["rev-parse", "--quiet", "--verify", GitOptions.HeadRevision];

    private static readonly GitRunOptions NoMatchOptions = new(
        ReadOnly: true,
        AcceptedExitCodes: [GitRunOptions.NoMatchExitCode]);

    public async Task<PickResult> PickAsync(
        RepositoryLocation location,
        PickKind kind,
        IReadOnlyList<string> commits,
        int? mainline,
        CancellationToken cancellationToken)
    {
        // A cancel before the start must not run a write that a short-lived process would finish before it is noticed.
        cancellationToken.ThrowIfCancellationRequested();
        if (commits.Count > MaxCommits)
        {
            throw new GitException(
                $"Too many commits for one {NameOf(kind)} ({commits.Count}); the limit is {MaxCommits}. " +
                "Split it into smaller ranges.");
        }

        var before = await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false);
        var wasInProgress = detector.Detect(location) != RepositoryOperation.None;
        OperationResult result;
        try
        {
            result = await RunAsync(location, kind, commits, mainline, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is GitCommandFailedException or GitTimeoutException or OperationCanceledException
            && !wasInProgress
            && detector.Detect(location) != RepositoryOperation.None)
        {
            await AbortLeftoverAsync(location, kind, before).ConfigureAwait(false);
            throw;
        }

        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new PickResult(PickOutcome.Conflicted, result.Head, before);
        }

        var outcome = result.Head == before ? PickOutcome.NothingToApply : PickOutcome.Completed;
        return new PickResult(outcome, result.Head, before);
    }

    private static string NameOf(PickKind kind) => kind == PickKind.CherryPick ? "cherry-pick" : "revert";

    private List<string> ArgumentsOf(PickKind kind, IReadOnlyList<string> commits, int? mainline, bool dropEmpty)
    {
        List<string> arguments = ["revert", NoEdit];
        if (kind == PickKind.CherryPick && dropEmpty)
        {
            arguments = ["cherry-pick", NoEdit, dropEmptyOption];
        }
        else if (kind == PickKind.CherryPick)
        {
            arguments = ["cherry-pick", NoEdit];
        }

        if (mainline is { } parent)
        {
            arguments.Add(MainlineOption);
            arguments.Add(parent.ToString(CultureInfo.InvariantCulture));
        }

        arguments.AddRange(commits);
        arguments.Add(GitOptions.EndOfOptions);
        return arguments;
    }

    // The empty option is newer than the minimum git: a usage error means it is unknown, and nothing has run yet.
    private async Task<OperationResult> RunAsync(
        RepositoryLocation location,
        PickKind kind,
        IReadOnlyList<string> commits,
        int? mainline,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operations
                .RunAsync(location, ArgumentsOf(kind, commits, mainline, true), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GitCommandFailedException exception) when (
            kind == PickKind.CherryPick && exception.ExitCode == UsageExitCode)
        {
            return await operations
                .RunAsync(location, ArgumentsOf(kind, commits, mainline, false), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // Not cancellable: a half-run sequence must be cleaned up even when the caller gave up. The original error is kept.
    private async Task AbortLeftoverAsync(RepositoryLocation location, PickKind kind, string? before)
    {
        var command = kind == PickKind.CherryPick ? "cherry-pick" : "revert";
        try
        {
            await operations.RunToCompletionAsync(location, [command, AbortOption], CancellationToken.None)
                .ConfigureAwait(false);

            // A call killed while committing makes the abort skip the rewind ("you seem to have moved HEAD"): put the
            // tip back where the pick started, which also clears the pick marker.
            var head = await ReadHeadAsync(location, CancellationToken.None).ConfigureAwait(false);
            if (before is not null && head != before)
            {
                await operations
                    .RunToCompletionAsync(location, [ResetCommand, MergeOption, before], CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (GitException)
        {
            // The original failure is the one to report (a stale lock or a hanging hook can leave the sequence).
        }
    }

    private async Task<string?> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, HeadArguments, NoMatchOptions, cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutputText.Trim() : null;
    }
}
