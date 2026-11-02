using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
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

    private const string LiteralPathspecs = "--literal-pathspecs";
    private const string UntrackedParent = "^3";

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);

    private static readonly string[] TopCommitArguments = ["rev-parse", "--quiet", "--verify", StashRef];


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

    public async Task<StashRestoreState> CheckRestoreAsync(
        RepositoryLocation location,
        StashEntry entry,
        StashFileChange file,
        CancellationToken cancellationToken) =>
        (await InspectAsync(location, entry, file, cancellationToken).ConfigureAwait(false)).State;

    public async Task<StashRestoreResult> RestoreFileAsync(
        RepositoryLocation location,
        StashEntry entry,
        StashFileChange file,
        CancellationToken cancellationToken)
    {
        var inspection = await InspectAsync(location, entry, file, cancellationToken).ConfigureAwait(false);
        if (inspection.State is not (StashRestoreState.Creates or StashRestoreState.Overwrites))
        {
            return new StashRestoreResult(inspection.State, null);
        }

        string? backup = null;
        if (inspection.State == StashRestoreState.Overwrites)
        {
            // Unfiltered, so the saved blob is the exact bytes on disk (no line-ending or smudge conversion), which
            // makes "git cat-file blob <id>" a byte-exact undo. It stays reachable by its id until git collects garbage.
            string[] saveArguments = ["hash-object", "-w", "--no-filters", "--", file.Change.Path];
            var saved = await runner
                .RunAsync(location.CommandPath, saveArguments, WriteOptions, cancellationToken)
                .ConfigureAwait(false);
            backup = saved.StandardOutputText.Trim();
        }

        string[] arguments =
        [
            LiteralPathspecs, "restore", $"--source={inspection.Source}", "--worktree", "--", file.Change.Path,
        ];
        await runner.RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken).ConfigureAwait(false);
        return new StashRestoreResult(inspection.State, backup);
    }

    private async Task<RestoreInspection> InspectAsync(
        RepositoryLocation location,
        StashEntry entry,
        StashFileChange file,
        CancellationToken cancellationToken)
    {
        if (await FindCurrentNameAsync(location, entry, cancellationToken).ConfigureAwait(false) is null)
        {
            return new RestoreInspection(StashRestoreState.Missing, string.Empty);
        }

        // Tracked changes live in the stash commit's tree, the files saved by --include-untracked in its third parent.
        var source = file.Origin == StashFileOrigin.Untracked ? $"{entry.CommitHash}{UntrackedParent}" : entry.CommitHash;
        string[] stashedArguments = ["rev-parse", "--quiet", "--verify", $"{source}:{file.Change.Path}"];
        var stashed = await runner
            .RunAsync(location.CommandPath, stashedArguments, NoMatchOptions, cancellationToken)
            .ConfigureAwait(false);
        if (stashed.ExitCode != 0)
        {
            return new RestoreInspection(StashRestoreState.NotInStash, source);
        }

        var local = Path.Combine(location.CommandPath, file.Change.Path);
        if (!File.Exists(local) && !Directory.Exists(local) && new FileInfo(local).LinkTarget is null)
        {
            return new RestoreInspection(StashRestoreState.Creates, source);
        }

        // Hashing applies the same clean filters as git add, so line-ending conversion does not fake a difference.
        string[] localArguments = ["hash-object", "--", file.Change.Path];
        var current = await runner
            .RunAsync(location.CommandPath, localArguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);
        var same = current.StandardOutputText.Trim() == stashed.StandardOutputText.Trim();
        return new RestoreInspection(same ? StashRestoreState.Identical : StashRestoreState.Overwrites, source);
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
        var unmergedBefore = await UnmergedFiles.ReadAsync(runner, location, cancellationToken).ConfigureAwait(false);
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
            var unmergedAfter = await UnmergedFiles.ReadAsync(runner, location, cancellationToken).ConfigureAwait(false);
            if (!UnmergedFiles.MadeConflicts(unmergedBefore, unmergedAfter))
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
