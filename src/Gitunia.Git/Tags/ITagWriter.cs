using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Tags;

/// <summary>
/// Creates and deletes tags and pushes or deletes one tag on a remote. Every call fails with the typed git exception
/// when git refuses; a push is one ref with no progress and no prompts, and git's own error is the failure text.
/// This is the seam the tag actions use so tests can replace git.
/// </summary>
public interface ITagWriter
{
    /// <summary>True when <paramref name="name"/> is a valid tag name (<c>git check-ref-format refs/tags/&lt;name&gt;</c>).</summary>
    Task<bool> IsValidNameAsync(RepositoryLocation location, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a tag at <paramref name="target"/>: annotated with <paramref name="message"/> when it is not null or
    /// empty, otherwise lightweight. No editor is ever opened.
    /// </summary>
    Task CreateAsync(
        RepositoryLocation location,
        string name,
        string target,
        string? message,
        CancellationToken cancellationToken);

    /// <summary>Deletes the local tag.</summary>
    Task DeleteAsync(RepositoryLocation location, string name, CancellationToken cancellationToken);

    /// <summary>The names of the configured remotes.</summary>
    Task<IReadOnlyList<string>> ListRemotesAsync(RepositoryLocation location, CancellationToken cancellationToken);

    /// <summary>Pushes <c>refs/tags/&lt;name&gt;</c> to <paramref name="remote"/>: that one ref, nothing else.</summary>
    Task PushAsync(RepositoryLocation location, string remote, string name, CancellationToken cancellationToken);

    /// <summary>Deletes <c>refs/tags/&lt;name&gt;</c> on <paramref name="remote"/>; the local tag stays.</summary>
    Task DeleteOnRemoteAsync(
        RepositoryLocation location,
        string remote,
        string name,
        CancellationToken cancellationToken);
}
