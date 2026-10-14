using Gitunia.Git.Execution;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Worktrees;

namespace Gitunia.Git.Refs;

/// <summary>Reads HEAD, branches, tags and worktrees of a repository.</summary>
public sealed class RefReader : IRefReader
{
    private static readonly string[] RefArguments = ["for-each-ref", $"--format={RefParser.Format}", GitRefPrefixes.Heads, GitRefPrefixes.Remotes, GitRefPrefixes.Tags];

    private static readonly string[] WorktreeArguments = ["worktree", "list", "--porcelain", GitOptions.NulTerminated];

    private static readonly string[] SymbolicHeadArguments = ["symbolic-ref", "-q", "HEAD"];

    private static readonly string[] VerifyHeadArguments = ["rev-parse", "--verify", "-q", "HEAD"];

    private readonly GitRunner _runner;

    public RefReader(GitRunner runner)
    {
        _runner = runner;
    }

    public async Task<ParseResult<GitRef>> ReadRefsAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(location.CommandPath, RefArguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return RefParser.Parse(result.StandardOutputText);
    }

    public async Task<ParseResult<WorktreeInfo>> ReadWorktreesAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(location.CommandPath, WorktreeArguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return WorktreeParser.Parse(result.StandardOutputText);
    }

    public async Task<HeadState> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var symbolic = await _runner.RunAsync(location.CommandPath, SymbolicHeadArguments, GitRunOptions.ReadOnlyRunAllowingNoMatch, cancellationToken).ConfigureAwait(false);
        var commit = await _runner.RunAsync(location.CommandPath, VerifyHeadArguments, GitRunOptions.ReadOnlyRunAllowingNoMatch, cancellationToken).ConfigureAwait(false);

        var reference = symbolic.StandardOutputText.Trim();
        var branchName = reference.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal) ? reference[GitRefPrefixes.Heads.Length..] : null;
        var hash = commit.ExitCode == 0 ? commit.StandardOutputText.Trim() : null;

        return (branchName, hash) switch
        {
            (null, _) => new HeadState(HeadKind.Detached, null, hash),
            (_, null) => new HeadState(HeadKind.Unborn, branchName, null),
            _ => new HeadState(HeadKind.Branch, branchName, hash),
        };
    }
}
