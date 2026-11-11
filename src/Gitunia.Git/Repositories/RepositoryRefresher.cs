using Gitunia.Git.Refs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Repositories;

/// <summary>
/// Recomputes a tab's <see cref="RepositorySnapshot"/> after a change. Only the sections the change can affect are read again,
/// and the result says which of them really differ, so the caller can keep scroll, selection and everything else as it is.
/// </summary>
public sealed class RepositoryRefresher : IRepositoryRefresher
{
    private readonly IRefReader _refReader;
    private readonly IStatusReader _statusReader;
    private readonly IRepositoryOperationDetector _operationDetector;

    public RepositoryRefresher(IRefReader refReader, IStatusReader statusReader, IRepositoryOperationDetector operationDetector)
    {
        _refReader = refReader;
        _statusReader = statusReader;
        _operationDetector = operationDetector;
    }

    public async Task<RepositoryRefreshResult> RefreshAsync(
        RepositoryLocation location,
        RepositorySnapshot? previous,
        RepositoryChangeKinds kinds,
        CancellationToken cancellationToken)
    {
        if (previous is null)
        {
            kinds = RepositoryChangeKinds.All;
        }

        var readHead = kinds.HasFlag(RepositoryChangeKinds.Head) || kinds.HasFlag(RepositoryChangeKinds.Refs);
        var readRefs = readHead || kinds.HasFlag(RepositoryChangeKinds.Worktrees);
        var readWorktrees = readRefs;
        var readStatus = readHead
            || kinds.HasFlag(RepositoryChangeKinds.Index)
            || kinds.HasFlag(RepositoryChangeKinds.WorkingTree)
            || kinds.HasFlag(RepositoryChangeKinds.Operation);

        var refsTask = readRefs ? _refReader.ReadRefsAsync(location, cancellationToken) : null;
        var worktreesTask = readWorktrees ? _refReader.ReadWorktreesAsync(location, cancellationToken) : null;
        var statusTask = readStatus ? _statusReader.ReadStatusAsync(location, includeIgnored: false, UntrackedFilesMode.All, cancellationToken) : null;

        var refs = refsTask is null ? previous!.Refs : (await refsTask.ConfigureAwait(false)).Items;
        var worktrees = worktreesTask is null ? previous!.Worktrees : (await worktreesTask.ConfigureAwait(false)).Items;
        var status = statusTask is null ? previous!.Status : await statusTask.ConfigureAwait(false);
        var operation = _operationDetector.Detect(location);

        // The status header already names the branch and its commit, so HEAD costs no process of its own (readHead implies readStatus).
        var head = readHead ? status.Branch.Head : previous!.Head;

        if (previous is null)
        {
            return new RepositoryRefreshResult(new RepositorySnapshot(head, refs, worktrees, status, operation), RepositorySections.All);
        }

        var changed = RepositorySections.None;
        if (head != previous.Head)
        {
            changed |= RepositorySections.Head;
        }
        else
        {
            head = previous.Head;
        }

        if (!refs.SequenceEqual(previous.Refs))
        {
            changed |= RepositorySections.Refs;
        }
        else
        {
            refs = previous.Refs;
        }

        if (!worktrees.SequenceEqual(previous.Worktrees))
        {
            changed |= RepositorySections.Worktrees;
        }
        else
        {
            worktrees = previous.Worktrees;
        }

        if (!AreSame(status, previous.Status))
        {
            changed |= RepositorySections.Status;
        }
        else
        {
            status = previous.Status;
        }

        if (operation != previous.Operation)
        {
            changed |= RepositorySections.Operation;
        }

        return new RepositoryRefreshResult(new RepositorySnapshot(head, refs, worktrees, status, operation), changed);
    }

    private static bool AreSame(StatusSnapshot current, StatusSnapshot previous) =>
        current.Branch == previous.Branch && current.Entries.SequenceEqual(previous.Entries);
}
