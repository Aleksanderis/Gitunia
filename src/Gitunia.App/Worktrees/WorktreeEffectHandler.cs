using Gitunia.Core.Text;
using Gitunia.Core.Worktrees;
using Gitunia.Git.Repositories;
using Gitunia.Git.Worktrees;

namespace Gitunia.App.Worktrees;

/// <summary>
/// Runs the worktree effects off the UI thread and posts exactly one result command for each: success with the text
/// for the status bar, failure with git's first error line, a request for the second remove answer, or cancellation.
/// Locating a worktree posts the resolved location or the failure. It never reads or changes state. Timeouts stay in
/// the git layer.
/// </summary>
/// <param name="writer">Runs the worktree commands.</param>
/// <param name="discovery">Resolves the path of a worktree to its location.</param>
/// <param name="dispatcher">Where the result commands go.</param>
/// <param name="tasks">Remembers the running work so the shutdown can wait for it; the default is a private set.</param>
public sealed class WorktreeEffectHandler(
    IWorktreeWriter writer,
    IRepositoryDiscovery discovery,
    IDispatcher dispatcher,
    ShutdownTasks? tasks = null)
{
    private readonly ShutdownTasks _tasks = tasks ?? new ShutdownTasks();

    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(WorktreeEffect effect, CancellationToken cancellationToken)
    {
        EffectWork.Start(
            dispatcher,
            token => WorkAsync(effect, token),
            () => new WorktreeOperationCancelled(effect.TabId),
            exception => new WorktreeOperationFailed(
                effect.TabId,
                $"{FailureOf(effect)}: {FailureText.Reason(exception)}"),
            cancellationToken,
            _tasks);
    }

    /// <summary>Starts resolving a worktree path and returns at once. A cancelled read (the tab was closed) posts
    /// nothing.</summary>
    public void Locate(LocateWorktreeEffect effect, CancellationToken cancellationToken)
    {
        _tasks.Run(() => LocateAsync(effect, cancellationToken));
    }

    private async Task LocateAsync(LocateWorktreeEffect effect, CancellationToken token)
    {
        WorktreeCommand result;
        try
        {
            var location = await discovery.DiscoverAsync(effect.Path, token).ConfigureAwait(false);
            result = new WorktreeLocated(effect.TabId, location, effect.IsSwitch);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            var verb = effect.IsSwitch ? "switch to" : "open";
            var text = $"Could not {verb} {effect.Path}: {FailureText.Reason(exception)}";
            result = new WorktreeLocateFailed(effect.TabId, text);
        }

        dispatcher.Post(result);
    }

    private Task<WorktreeCommand> WorkAsync(WorktreeEffect effect, CancellationToken token) => effect switch
    {
        AddWorktreeEffect add => AddAsync(add, token),
        LockWorktreeEffect locked => LockAsync(locked, token),
        PruneWorktreesEffect prune => PruneAsync(prune, token),
        RemoveWorktreeEffect remove => RemoveAsync(remove, token),
        UnlockWorktreeEffect unlock => UnlockAsync(unlock, token),
        _ => throw new UnhandledEffectException(effect),
    };

    private async Task<WorktreeCommand> AddAsync(AddWorktreeEffect effect, CancellationToken token)
    {
        await writer.AddAsync(effect.Location, effect.Request, token).ConfigureAwait(false);
        var text = $"Added worktree {effect.Request.Path} on {effect.Request.Branch}";

        return new WorktreeOperationSucceeded(effect.TabId, text, effect.OpenAfter ? effect.Request.Path : null);
    }

    // Without force, a worktree with changes is left alone and the user is asked what to do with them.
    private async Task<WorktreeCommand> RemoveAsync(RemoveWorktreeEffect effect, CancellationToken token)
    {
        if (!effect.Force)
        {
            var changes = await writer.ListChangesAsync(effect.Path, token).ConfigureAwait(false);
            if (changes.Count > 0)
            {
                return new WorktreeRemoveNeedsForce(effect.TabId, effect.Path, [.. changes]);
            }
        }

        await writer.RemoveAsync(effect.Location, effect.Path, effect.Force, token).ConfigureAwait(false);
        var suffix = effect.Force ? " and its uncommitted changes" : string.Empty;

        return new WorktreeOperationSucceeded(effect.TabId, $"Removed worktree {effect.Path}{suffix}", null);
    }

    private async Task<WorktreeCommand> LockAsync(LockWorktreeEffect effect, CancellationToken token)
    {
        await writer.LockAsync(effect.Location, effect.Path, effect.Reason, token).ConfigureAwait(false);
        var reason = effect.Reason.Length > 0 ? $" ({effect.Reason})" : string.Empty;

        return new WorktreeOperationSucceeded(effect.TabId, $"Locked worktree {effect.Path}{reason}", null);
    }

    private async Task<WorktreeCommand> UnlockAsync(UnlockWorktreeEffect effect, CancellationToken token)
    {
        await writer.UnlockAsync(effect.Location, effect.Path, token).ConfigureAwait(false);

        return new WorktreeOperationSucceeded(effect.TabId, $"Unlocked worktree {effect.Path}", null);
    }

    private async Task<WorktreeCommand> PruneAsync(PruneWorktreesEffect effect, CancellationToken token)
    {
        var removed = await writer.PruneAsync(effect.Location, token).ConfigureAwait(false);
        var text = removed.Count == 1 ? "Pruned 1 worktree entry" : $"Pruned {removed.Count} worktree entries";

        return new WorktreeOperationSucceeded(effect.TabId, text, null);
    }

    private static string FailureOf(WorktreeEffect effect) => effect switch
    {
        AddWorktreeEffect => "Could not add the worktree",
        LockWorktreeEffect => "Could not lock the worktree",
        PruneWorktreesEffect => "Could not prune",
        RemoveWorktreeEffect => "Could not remove the worktree",
        UnlockWorktreeEffect => "Could not unlock the worktree",
        _ => throw new UnhandledEffectException(effect),
    };
}
