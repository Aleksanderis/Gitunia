using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Answers a message search from memory: every commit whose id is in <see cref="Hits"/>.</summary>
public sealed class FakeCommitMessageSearcher : ICommitMessageSearcher
{
    private int _callCount;

    public HashSet<ObjectId> Hits { get; } = [];

    /// <summary>When set, the search waits for it, so a test can type more before the answer arrives.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Thrown by the search, to play a failing git.</summary>
    public GitException? Failure { get; set; }

    public int CallCount => Volatile.Read(ref _callCount);

    public string? LastText { get; private set; }

    public async Task<IReadOnlySet<ObjectId>> SearchAsync(
        RepositoryLocation location,
        string text,
        CancellationToken cancellationToken)
    {
        LastText = text;
        Interlocked.Increment(ref _callCount);
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Failure is null ? Hits : throw Failure;
    }
}
