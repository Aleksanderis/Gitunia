using System.Runtime.CompilerServices;
using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Streams a commit log from memory, so loader tests need no git repository. Commits are listed newest first, as git prints them. The order, revisions and warnings arguments are ignored.</summary>
public sealed class FakeCommitLogReader : ICommitLogReader
{
    private static readonly DateTimeOffset CommitTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private int _nextNumber = 1;

    /// <summary>The log, newest first.</summary>
    public List<CommitInfo> Commits { get; } = [];

    /// <summary>Thrown when a read starts, to play a git failure.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Thrown when the tip decorations are read, to play a failure of only that call.</summary>
    public Exception? TipFailure { get; set; }

    /// <summary>The number of log streams started so far.</summary>
    public int ReadCount { get; private set; }

    /// <summary>A linear history of <paramref name="count"/> commits; subjects are "commit 1" (oldest) to "commit N" (the tip).</summary>
    public static FakeCommitLogReader Chain(int count)
    {
        var reader = new FakeCommitLogReader();
        for (var number = 1; number <= count; number++)
        {
            reader.AddOnTop($"commit {number}");
        }

        return reader;
    }

    /// <summary>Adds a commit on top of the current tip (no parent for the first one) and returns it.</summary>
    public CommitInfo AddOnTop(string subject, params CommitDecoration[] decorations)
    {
        IReadOnlyList<ObjectId> parents = Commits.Count == 0 ? [] : [Commits[0].Id];
        var commit = new CommitInfo(FakeObjectIds.Of(_nextNumber++), parents, "Author", "author@example.com", CommitTime, subject, decorations);
        Commits.Insert(0, commit);
        return commit;
    }

    public async IAsyncEnumerable<CommitInfo> ReadAsync(
        RepositoryLocation location,
        CommitOrder order,
        IReadOnlyList<string> revisions,
        ICollection<string>? warnings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        if (Failure is not null)
        {
            throw Failure;
        }

        foreach (var commit in Commits.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return commit;
        }

        // Keeps this an async iterator without an await in the loop.
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public Task<int> CountAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Failure is null ? Task.FromResult(Commits.Count) : Task.FromException<int>(Failure);
    }

    public Task<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>> ReadTipDecorationsAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failure = TipFailure ?? Failure;
        if (failure is not null)
        {
            return Task.FromException<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>>(failure);
        }

        var decorated = Commits.Where(commit => commit.Decorations.Count > 0).ToDictionary(commit => commit.Id, commit => commit.Decorations);
        return Task.FromResult(decorated);
    }
}
