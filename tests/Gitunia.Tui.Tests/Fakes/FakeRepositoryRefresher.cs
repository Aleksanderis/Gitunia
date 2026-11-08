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
    private int _woundDown;

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
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public Exception? Failure { get; set; }

    /// <summary>How long every refresh takes, to play a slow repository.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public TimeSpan Delay { get; set; }

    /// <summary>How long a cancelled refresh takes to wind down, to play a git process that is still being killed.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public TimeSpan WindDown { get; set; }

    /// <summary>The number of cancelled refreshes that have finished winding down.</summary>
    public int WoundDownCount => Volatile.Read(ref _woundDown);

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
        var result = new RepositoryRefreshResult(_snapshot, changed);

        return Delay == TimeSpan.Zero ? Task.FromResult(result) : DelayedAsync(result, cancellationToken);
    }

    private async Task<RepositoryRefreshResult> DelayedAsync(
        RepositoryRefreshResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await Task.Delay(WindDown, CancellationToken.None);
            Interlocked.Increment(ref _woundDown);

            throw;
        }

        return result;
    }
}
