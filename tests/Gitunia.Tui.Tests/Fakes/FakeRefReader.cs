using Gitunia.Git;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Worktrees;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Answers HEAD, ref and worktree reads from memory. A part with a failure set throws it, like git failing.</summary>
public sealed class FakeRefReader : IRefReader
{
    /// <summary>What HEAD points at.</summary>
    public HeadState Head { get; set; } = new(HeadKind.Branch, "main", null);

    /// <summary>The branches and tags.</summary>
    public List<GitRef> Refs { get; } = [];

    /// <summary>The worktrees.</summary>
    public List<WorktreeInfo> Worktrees { get; } = [];

    /// <summary>Thrown by the HEAD read.</summary>
    public Exception? HeadFailure { get; set; }

    /// <summary>Thrown by the ref read.</summary>
    public Exception? RefsFailure { get; set; }

    /// <summary>Thrown by the worktree read.</summary>
    public Exception? WorktreesFailure { get; set; }

    public Task<ParseResult<GitRef>> ReadRefsAsync(RepositoryLocation location, CancellationToken cancellationToken) =>
        Answer(RefsFailure, new ParseResult<GitRef>(Refs.ToArray(), []), cancellationToken);

    public Task<ParseResult<WorktreeInfo>> ReadWorktreesAsync(RepositoryLocation location, CancellationToken cancellationToken) =>
        Answer(WorktreesFailure, new ParseResult<WorktreeInfo>(Worktrees.ToArray(), []), cancellationToken);

    public Task<HeadState> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken) =>
        Answer(HeadFailure, Head, cancellationToken);

    private static Task<TResult> Answer<TResult>(Exception? failure, TResult result, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<TResult>(cancellationToken);
        }

        return failure is null ? Task.FromResult(result) : Task.FromException<TResult>(failure);
    }
}
