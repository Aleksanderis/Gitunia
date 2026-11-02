using Gitunia.Git;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Answers stash reads from memory, or throws <see cref="Failure"/> when it is set, like git failing.</summary>
public sealed class FakeStashReader : IStashReader
{
    /// <summary>The stash stack, newest first.</summary>
    // ReSharper disable once CollectionNeverUpdated.Global : fake seed list for tests that need stashes
    public List<StashEntry> Stashes { get; } = [];

    /// <summary>Thrown by every read.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public Exception? Failure { get; set; }

    public Task<ParseResult<StashEntry>> ReadStashesAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<ParseResult<StashEntry>>(cancellationToken);
        }

        if (Failure is null)
        {
            return Task.FromResult(new ParseResult<StashEntry>(Stashes.ToArray(), []));
        }

        return Task.FromException<ParseResult<StashEntry>>(Failure);
    }
}
