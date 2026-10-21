using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tags;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// A tag writer whose remote calls (push and delete on a remote) fail with the given git error, as a remote that
/// needs credentials does; every other call goes to the real writer.
/// </summary>
public sealed class AuthFailingTagWriter(ITagWriter inner, string standardError) : ITagWriter
{
    public Task<bool> IsValidNameAsync(
        RepositoryLocation location,
        string name,
        CancellationToken cancellationToken) =>
        inner.IsValidNameAsync(location, name, cancellationToken);

    public Task CreateAsync(
        RepositoryLocation location,
        string name,
        string target,
        string? message,
        CancellationToken cancellationToken) =>
        inner.CreateAsync(location, name, target, message, cancellationToken);

    public Task DeleteAsync(RepositoryLocation location, string name, CancellationToken cancellationToken) =>
        inner.DeleteAsync(location, name, cancellationToken);

    public Task<IReadOnlyList<string>> ListRemotesAsync(
        RepositoryLocation location,
        CancellationToken cancellationToken) =>
        inner.ListRemotesAsync(location, cancellationToken);

    public Task PushAsync(
        RepositoryLocation location,
        string remote,
        string name,
        CancellationToken cancellationToken) =>
        Task.FromException(new GitCommandFailedException(["push"], 128, standardError));

    public Task DeleteOnRemoteAsync(
        RepositoryLocation location,
        string remote,
        string name,
        CancellationToken cancellationToken) =>
        Task.FromException(new GitCommandFailedException(["push"], 128, standardError));
}
