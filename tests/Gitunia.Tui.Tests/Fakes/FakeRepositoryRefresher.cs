using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// Returns a snapshot from memory, so tab refresher tests need no git repository. A refresh reports every section as changed once after
/// <see cref="Snapshot"/> was replaced, and none otherwise. Set <see cref="Failure"/> to play a git failure.
/// </summary>
public sealed class FakeRepositoryRefresher : IRepositoryRefresher
{
    private static readonly HeadState MainBranch = new(HeadKind.Branch, "main", null);

    private RepositorySnapshot _snapshot = new(MainBranch, [], [], new StatusSnapshot(new StatusBranch(MainBranch, null, null, null), [], []), RepositoryOperation.None);
    private RepositorySections _changed = RepositorySections.None;

    /// <summary>What the next refresh returns; replacing it marks every section as changed for the next refresh.</summary>
    public RepositorySnapshot Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            _changed = RepositorySections.All;
        }
    }

    /// <summary>Thrown by every refresh.</summary>
    public Exception? Failure { get; set; }

    /// <summary>The number of refreshes so far.</summary>
    public int RefreshCount { get; private set; }

    public Task<RepositoryRefreshResult> RefreshAsync(RepositoryLocation location, RepositorySnapshot? previous, RepositoryChangeKinds kinds, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<RepositoryRefreshResult>(cancellationToken);
        }

        RefreshCount++;
        if (Failure is not null)
        {
            return Task.FromException<RepositoryRefreshResult>(Failure);
        }

        var changed = previous is null ? RepositorySections.All : _changed;
        _changed = RepositorySections.None;
        return Task.FromResult(new RepositoryRefreshResult(_snapshot, changed));
    }
}
