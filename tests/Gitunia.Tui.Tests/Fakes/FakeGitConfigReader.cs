using Gitunia.Git.Config;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>A git config reader that answers from memory, so tests need no repository on disk.</summary>
public sealed class FakeGitConfigReader : IGitConfigReader
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake setting kept for tests that configure it
    public bool RebaseAutoStash { get; set; }

    public Task<bool> ReadRebaseAutoStashAsync(RepositoryLocation location, CancellationToken cancellationToken) =>
        Task.FromResult(RebaseAutoStash);
}
