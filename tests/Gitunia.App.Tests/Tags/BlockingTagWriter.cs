using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tags;

namespace Gitunia.App.Tests.Tags;

/// <summary>A tag writer whose delete waits until its token is cancelled, to test cancellation and busy
/// state.</summary>
public sealed class BlockingTagWriter : ITagWriter
{
    private volatile bool _finished;

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationToken Token { get; private set; }

    public bool Finished => _finished;

    public async Task DeleteAsync(RepositoryLocation location, string name, CancellationToken cancellationToken)
    {
        Token = cancellationToken;
        Started.TrySetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        finally
        {
            _finished = true;
        }
    }

    public Task<bool> IsValidNameAsync(RepositoryLocation location, string name, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task CreateAsync(
        RepositoryLocation location,
        string name,
        string target,
        string? message,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<string>> ListRemotesAsync(
        RepositoryLocation location,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task PushAsync(
        RepositoryLocation location,
        string remote,
        string name,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DeleteOnRemoteAsync(
        RepositoryLocation location,
        string remote,
        string name,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
