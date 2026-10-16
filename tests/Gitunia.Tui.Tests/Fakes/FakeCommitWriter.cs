using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Records the commits instead of running git; can fail, or wait until released (and then honour cancellation).</summary>
public sealed class FakeCommitWriter : ICommitWriter
{
    private readonly TaskCompletionSource _gate = new();

    public List<CommitRequest> Requests { get; } = [];

    /// <summary>The message <see cref="ReadHeadMessageAsync"/> returns; null means no commit yet.</summary>
    public string? HeadMessage { get; set; } = "Previous subject\n\nPrevious body";

    /// <summary>Thrown by the next commits when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When true, commits wait until <see cref="Release"/> or until they are cancelled.</summary>
    public bool IsHeld { get; set; }

    public void Release() => _gate.TrySetResult();

    public async Task<CommitResult> CommitAsync(RepositoryLocation location, CommitRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (IsHeld)
        {
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }

        return new CommitResult("2222222222222222222222222222222222222222", request.Amend ? "1111111111111111111111111111111111111111" : null);
    }

    public Task<string?> ReadHeadMessageAsync(RepositoryLocation location, CancellationToken cancellationToken) =>
        Task.FromResult(HeadMessage);
}
