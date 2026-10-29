using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Git.Stash;

/// <summary>Writes the stash stack with the git CLI.</summary>
public sealed class StashWriter(GitRunner runner) : IStashWriter
{
    private const string StashRef = "refs/stash";
    private const string MessageOption = "-m";
    private const string IncludeUntrackedOption = "--include-untracked";

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);

    private static readonly string[] TopCommitArguments = ["rev-parse", "--quiet", "--verify", StashRef];

    private static readonly string[] UnmergedArguments = ["ls-files", "--unmerged"];

    private static readonly string[] ListArguments = ["stash", "list", $"--format={StashParser.Format}"];

    private static readonly GitRunOptions NoMatchOptions = new(
        Timeout: WriteTimeout,
        ReadOnly: true,
        AcceptedExitCodes: [GitRunOptions.NoMatchExitCode]);

    private static readonly GitRunOptions WriteOptions = new(Timeout: WriteTimeout);

    public async Task<StashResult> PushAsync(
        RepositoryLocation location,
        string message,
        bool includeUntracked,
        CancellationToken cancellationToken)
    {
        var before = await ReadTopAsync(location, cancellationToken).ConfigureAwait(false);
        List<string> arguments = ["stash", "push", "--quiet"];
        if (includeUntracked)
        {
            arguments.Add(IncludeUntrackedOption);
        }

        if (message.Length > 0)
        {
            arguments.Add(MessageOption);
            arguments.Add(message);
        }

        await runner.RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken).ConfigureAwait(false);

        // Git exits 0 with "No local changes to save", so a new top entry is the only proof that a stash was made.
        var after = await ReadTopAsync(location, cancellationToken).ConfigureAwait(false);
        if (after is null || after == before)
        {
            return new StashResult(StashOutcome.NothingToStash, null);
        }

        return new StashResult(StashOutcome.Done, after);
    }

    public Task<StashResult> ApplyAsync(
        RepositoryLocation location,
        StashEntry entry,
        CancellationToken cancellationToken) =>
        ApplyOrPopAsync(location, entry, "apply", cancellationToken);

    public Task<StashResult> PopAsync(
        RepositoryLocation location,
        StashEntry entry,
        CancellationToken cancellationToken) =>
        ApplyOrPopAsync(location, entry, "pop", cancellationToken);

    public async Task<StashResult> DropAsync(
        RepositoryLocation location,
        StashEntry entry,
        CancellationToken cancellationToken)
    {
        var name = await FindCurrentNameAsync(location, entry, cancellationToken).ConfigureAwait(false);
        if (name is null)
        {
            return new StashResult(StashOutcome.Missing, entry.CommitHash);
        }

        string[] arguments = ["stash", "drop", "--quiet", name];
        await runner.RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken).ConfigureAwait(false);
        return new StashResult(StashOutcome.Done, entry.CommitHash);
    }

    private async Task<StashResult> ApplyOrPopAsync(
        RepositoryLocation location,
        StashEntry entry,
        string command,
        CancellationToken cancellationToken)
    {
        var name = await FindCurrentNameAsync(location, entry, cancellationToken).ConfigureAwait(false);
        if (name is null)
        {
            return new StashResult(StashOutcome.Missing, entry.CommitHash);
        }

        string[] arguments = ["stash", command, "--quiet", name];
        var unmergedBefore = await ReadUnmergedAsync(location, cancellationToken).ConfigureAwait(false);
        try
        {
            await runner
                .RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken)
                .ConfigureAwait(false);
            return new StashResult(StashOutcome.Done, entry.CommitHash);
        }
        catch (GitCommandFailedException)
        {
            // Only a change of the unmerged set proves this call made the conflict; files that were already conflicted
            // (a stopped merge) mean git refused, and its message is the answer.
            var unmergedAfter = await ReadUnmergedAsync(location, cancellationToken).ConfigureAwait(false);
            if (unmergedAfter.Length == 0 || unmergedAfter == unmergedBefore)
            {
                throw;
            }

            // Conflict markers are in the tree and the stash is kept: git drops it only after a clean pop.
            return new StashResult(StashOutcome.Conflicted, entry.CommitHash);
        }
    }

    private async Task<string?> ReadTopAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, TopCommitArguments, NoMatchOptions, cancellationToken)
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

    private async Task<string?> FindCurrentNameAsync(
        RepositoryLocation location,
        StashEntry entry,
        CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, ListArguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);
        return StashParser.Parse(result.StandardOutputText).Items
            .FirstOrDefault(candidate => candidate.CommitHash == entry.CommitHash)?.Name;
    }
}
