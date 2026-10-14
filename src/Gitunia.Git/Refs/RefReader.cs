using Gitunia.Git.Execution;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Worktrees;

namespace Gitunia.Git.Refs;

/// <summary>Reads branches, tags and worktrees of a repository.</summary>
public static class RefReader
{
    private static readonly GitRunOptions PollingOptions = new(ReadOnly: true);

    public static async Task<ParseResult<GitRef>> ReadRefsAsync(Repository repository, CancellationToken cancellationToken)
    {
        var result = await repository.RunAsync(
            ["for-each-ref", $"--format={RefParser.Format}", GitRefPrefixes.Heads, GitRefPrefixes.Remotes, GitRefPrefixes.Tags],
            PollingOptions,
            cancellationToken).ConfigureAwait(false);
        return RefParser.Parse(result.StandardOutputText);
    }

    public static async Task<ParseResult<WorktreeInfo>> ReadWorktreesAsync(Repository repository, CancellationToken cancellationToken)
    {
        var result = await repository.RunAsync(["worktree", "list", "--porcelain", GitOptions.NulTerminated], PollingOptions, cancellationToken).ConfigureAwait(false);
        return WorktreeParser.Parse(result.StandardOutputText);
    }
}
