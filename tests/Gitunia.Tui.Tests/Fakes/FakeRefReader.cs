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
    // ReSharper disable once CollectionNeverUpdated.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public List<GitRef> Refs { get; } = [];

    /// <summary>The worktrees.</summary>
    // ReSharper disable once CollectionNeverUpdated.Global : Fake seed list for tests that need worktrees
    public List<WorktreeInfo> Worktrees { get; } = [];

    /// <summary>Thrown by the HEAD read.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public Exception? HeadFailure { get; set; }

    /// <summary>Thrown by the ref read.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake failure switch kept for tests
    public Exception? RefsFailure { get; set; }

    /// <summary>Thrown by the worktree read.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
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
