using System.Globalization;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Pick;

/// <summary>
/// Cherry-picks and reverts with the git CLI through <see cref="OperationRunner"/>, so a stop on conflicts is a result.
/// <c>--no-edit</c> keeps git from opening an editor for the message. A cherry-pick passes <c>--empty=drop</c>: a
/// commit whose changes HEAD has already is skipped instead of stopping the sequence for a decision (when all of them
/// are, the result is <see cref="PickOutcome.NothingToApply"/>); a git too old to know the option (usage error, before
/// anything ran) gets the call again without it, and an empty pick then stops like git's default. The commits are
/// hashes from the UI; the end-of-options marker follows them.
/// </summary>
public sealed class PickWriter(GitRunner runner, OperationRunner operations) : IPickWriter
{
    private const string NoEdit = "--no-edit";
    private const string DropEmpty = "--empty=drop";
    private const string MainlineOption = "-m";
    private const int UsageExitCode = 129;

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
        var before = await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false);
        var result = await RunAsync(location, kind, commits, mainline, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new PickResult(PickOutcome.Conflicted, result.Head, before);
        }

        var outcome = result.Head == before ? PickOutcome.NothingToApply : PickOutcome.Completed;
        return new PickResult(outcome, result.Head, before);
    }

    private static List<string> ArgumentsOf(PickKind kind, IReadOnlyList<string> commits, int? mainline, bool dropEmpty)
    {
        List<string> arguments = kind == PickKind.CherryPick
            ? dropEmpty ? ["cherry-pick", NoEdit, DropEmpty] : ["cherry-pick", NoEdit]
            : ["revert", NoEdit];
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

    private async Task<string?> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, HeadArguments, NoMatchOptions, cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutputText.Trim() : null;
    }
}
