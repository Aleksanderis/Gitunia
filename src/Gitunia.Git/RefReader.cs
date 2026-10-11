namespace Gitunia.Git;

/// <summary>Reads branches, tags and worktrees of a repository.</summary>
public static class RefReader
{
    private static readonly GitRunOptions PollingOptions = new(ReadOnly: true);

    public static async Task<ParseResult<GitRef>> ReadRefsAsync(Repo repo, CancellationToken cancellationToken)
    {
        GitResult result = await repo.RunAsync(
            ["for-each-ref", "--format=" + RefParser.Format, "refs/heads", "refs/remotes", "refs/tags"],
            PollingOptions,
            cancellationToken).ConfigureAwait(false);
        return RefParser.Parse(result.StandardOutputText);
    }

    public static async Task<ParseResult<WorktreeInfo>> ReadWorktreesAsync(Repo repo, CancellationToken cancellationToken)
    {
        GitResult result = await repo.RunAsync(["worktree", "list", "--porcelain", "-z"], PollingOptions, cancellationToken).ConfigureAwait(false);
        return WorktreeParser.Parse(result.StandardOutputText);
    }
}
