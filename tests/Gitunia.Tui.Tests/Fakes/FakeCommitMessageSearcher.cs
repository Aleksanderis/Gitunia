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
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Thrown by the search, to play a failing git.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public GitException? Failure { get; set; }

    /// <summary>When set, the search ignores cancellation, like a process that ended just as it was killed.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public bool IgnoreCancellation { get; set; }

    /// <summary>Completes when the search returned or threw.</summary>
    public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationToken TokenOf(int call)
    {
        lock (Tokens)
        {
            return Tokens[call];
        }
    }

    public int CallCount => Volatile.Read(ref _callCount);

    /// <summary>The token of every call, in call order.</summary>
    public List<CancellationToken> Tokens { get; } = [];

    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public string? LastText { get; private set; }

    public async Task<IReadOnlySet<ObjectId>> SearchAsync(
        RepositoryLocation location,
        string text,
        CancellationToken cancellationToken)
    {
        LastText = text;
        lock (Tokens)
        {
            Tokens.Add(cancellationToken);
        }

        Interlocked.Increment(ref _callCount);
        if (Gate is { } gate)
        {
            var token = IgnoreCancellation ? CancellationToken.None : cancellationToken;
            await gate.Task.WaitAsync(token).ConfigureAwait(false);
        }

        Finished.TrySetResult();

        return Failure is null ? Hits : throw Failure;
    }
}
