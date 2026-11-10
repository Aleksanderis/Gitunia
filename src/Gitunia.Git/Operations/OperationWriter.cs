using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Operations;

/// <summary>
/// Continue, abort and skip for merge, rebase, cherry-pick and revert. <c>-c core.editor=true</c> keeps git from
/// opening
/// an editor for the prepared message (the sequence editor is not involved: no todo list is edited here); the config
/// is passed per call and never written. A rebase that stops on the next commit comes back as
/// <see cref="OperationOutcome.Conflicted"/>.
/// </summary>
public sealed class OperationWriter(OperationRunner runner) : IOperationWriter
{
    private const string ContinueOption = "--continue";
    private const string AbortOption = "--abort";
    private const string SkipOption = "--skip";
    private const string MergeCommand = "merge";
    private const string RebaseCommand = "rebase";
    private const string CherryPickCommand = "cherry-pick";
    private const string RevertCommand = "revert";
    private const string ConfigOption = "-c";
    private const string NoEditor = "core.editor=true";

    public async Task<OperationResult> ContinueAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken)
    {
        // The staged resolution is what gets committed. When it leaves nothing (the picked change is already in
        // HEAD), git refuses with "now empty"; the commit is dropped instead, as a rebase drops one. Check then act:
        // an index change by another process in between is accepted.
        if (operation is RepositoryOperation.CherryPick or RepositoryOperation.Revert
            && await runner.HasNothingToCommitAsync(location, cancellationToken).ConfigureAwait(false))
        {
            var skipped = await RunAsync(location, operation, SkipOption, cancellationToken).ConfigureAwait(false);
            return skipped.Outcome == OperationOutcome.Completed
                ? skipped with { Outcome = OperationOutcome.Emptied }
                : skipped;
        }

        return await RunAsync(location, operation, ContinueOption, cancellationToken).ConfigureAwait(false);
    }

    public Task<OperationResult> AbortAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken) =>
        runner.RunToCompletionAsync(location, ArgumentsOf(operation, AbortOption), cancellationToken);

    public Task<OperationResult> SkipAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken)
    {
        if (operation == RepositoryOperation.Merge)
        {
            throw new NotSupportedException("A merge cannot be skipped");
        }

        return RunAsync(location, operation, SkipOption, cancellationToken);
    }

    private Task<OperationResult> RunAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        string option,
        CancellationToken cancellationToken)
        => runner.RunAsync(location, ArgumentsOf(operation, option), cancellationToken);

    private static string[] ArgumentsOf(RepositoryOperation operation, string option) =>
        [ConfigOption, NoEditor, CommandOf(operation), option];

    private static string CommandOf(RepositoryOperation operation) => operation switch
    {
        RepositoryOperation.Merge => MergeCommand,
        RepositoryOperation.Rebase => RebaseCommand,
        RepositoryOperation.CherryPick => CherryPickCommand,
        RepositoryOperation.Revert => RevertCommand,
        _ => throw new NotSupportedException($"The {operation} operation cannot be continued here"),
    };
}
