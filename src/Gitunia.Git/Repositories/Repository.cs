using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Repositories;

/// <summary>
/// A repository or worktree bound to an explicit path. Every git call made through it runs against that path;
/// nothing depends on the process working directory.
/// </summary>
public sealed class Repository
{
    private const string BranchPrefix = "refs/heads/";

    public Repository(GitRunner runner, RepositoryLocation location)
    {
        Runner = runner;
        Location = location;
    }

    public GitRunner Runner { get; }

    public RepositoryLocation Location { get; }

    /// <summary>Discovers the repository containing <paramref name="path"/> and binds a handle to it.</summary>
    public static async Task<Repository> OpenAsync(GitRunner runner, string path, CancellationToken cancellationToken)
    {
        var location = await RepositoryDiscovery.DiscoverAsync(runner, path, cancellationToken).ConfigureAwait(false);
        return new Repository(runner, location);
    }

    /// <summary>Runs git against this repository or worktree.</summary>
    public Task<GitResult> RunAsync(IReadOnlyList<string> arguments, GitRunOptions? options = null, CancellationToken cancellationToken = default) =>
        Runner.RunAsync(Location.CommandPath, arguments, options, cancellationToken);

    public async Task<HeadState> ReadHeadAsync(CancellationToken cancellationToken)
    {
        var options = new GitRunOptions(ReadOnly: true, AcceptedExitCodes: [1]);
        var symbolic = await RunAsync(["symbolic-ref", "-q", "HEAD"], options, cancellationToken).ConfigureAwait(false);
        var commit = await RunAsync(["rev-parse", "--verify", "-q", "HEAD"], options, cancellationToken).ConfigureAwait(false);

        var reference = symbolic.StandardOutputText.Trim();
        var branchName = reference.StartsWith(BranchPrefix, StringComparison.Ordinal) ? reference[BranchPrefix.Length..] : null;
        var hash = commit.ExitCode == 0 ? commit.StandardOutputText.Trim() : null;

        return (branchName, hash) switch
        {
            (null, _) => new HeadState(HeadKind.Detached, null, hash),
            (_, null) => new HeadState(HeadKind.Unborn, branchName, null),
            _ => new HeadState(HeadKind.Branch, branchName, hash),
        };
    }

    /// <summary>Detects a stopped multi-step operation from the state files in this worktree's git directory.</summary>
    public RepositoryOperation DetectOperation()
    {
        var gitDirectory = Location.GitDirectory;
        if (Directory.Exists(Path.Combine(gitDirectory, "rebase-merge")))
        {
            return RepositoryOperation.Rebase;
        }

        if (Directory.Exists(Path.Combine(gitDirectory, "rebase-apply")))
        {
            return File.Exists(Path.Combine(gitDirectory, "rebase-apply", "applying"))
                ? RepositoryOperation.ApplyMailbox
                : RepositoryOperation.Rebase;
        }

        if (File.Exists(Path.Combine(gitDirectory, "MERGE_HEAD")))
        {
            return RepositoryOperation.Merge;
        }

        if (File.Exists(Path.Combine(gitDirectory, "CHERRY_PICK_HEAD")))
        {
            return RepositoryOperation.CherryPick;
        }

        if (File.Exists(Path.Combine(gitDirectory, "REVERT_HEAD")))
        {
            return RepositoryOperation.Revert;
        }

        var sequencerTodo = Path.Combine(gitDirectory, "sequencer", "todo");
        if (File.Exists(sequencerTodo))
        {
            try
            {
                var firstCommand = File.ReadLines(sequencerTodo).FirstOrDefault(line => line.Length > 0 && line[0] != '#') ?? string.Empty;
                return firstCommand.StartsWith("revert", StringComparison.Ordinal) ? RepositoryOperation.Revert : RepositoryOperation.CherryPick;
            }
            catch (IOException)
            {
                // The operation finished while we looked.
                return RepositoryOperation.None;
            }
        }

        return File.Exists(Path.Combine(gitDirectory, "BISECT_LOG")) ? RepositoryOperation.Bisect : RepositoryOperation.None;
    }
}
