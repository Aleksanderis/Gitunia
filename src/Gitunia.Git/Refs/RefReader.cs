using Gitunia.Git.Execution;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Worktrees;

namespace Gitunia.Git.Refs;

/// <summary>Reads branches, tags and worktrees of a repository.</summary>
public static class RefReader
{
    private static readonly string[] RefArguments = ["for-each-ref", $"--format={RefParser.Format}", GitRefPrefixes.Heads, GitRefPrefixes.Remotes, GitRefPrefixes.Tags];

    private static readonly string[] WorktreeArguments = ["worktree", "list", "--porcelain", GitOptions.NulTerminated];

    public static async Task<ParseResult<GitRef>> ReadRefsAsync(Repository repository, CancellationToken cancellationToken)
    {
        var result = await repository.RunAsync(
            RefArguments,
            GitRunOptions.ReadOnlyRun,
            cancellationToken).ConfigureAwait(false);
        return RefParser.Parse(result.StandardOutputText);
    }

    public static async Task<ParseResult<WorktreeInfo>> ReadWorktreesAsync(Repository repository, CancellationToken cancellationToken)
    {
        var result = await repository.RunAsync(WorktreeArguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return WorktreeParser.Parse(result.StandardOutputText);
    }
}
