using System.Collections.Concurrent;
using Gitunia.Core.Status;
using Gitunia.Core.Sync;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.App.Sync;

/// <summary>
/// Runs the fetch, pull, push and remote branch delete effects off the UI thread, reports git's progress as commands
/// and posts exactly one result command for each effect: success with the text for the status bar, failure with git's
/// reason (and the terminal retry when the call lacked credentials, or the rejection a force push can answer), or
/// cancellation. The cancel of a running effect cancels its own token and git is killed. It never reads or changes
/// state. Timeouts stay in the git layer.
/// </summary>
/// <param name="writer">Runs fetch, pull, push and the remote branch delete.</param>
/// <param name="manager">Lists the remotes.</param>
/// <param name="ancestry">Compares a local tip with the remote one.</param>
/// <param name="dispatcher">Where the progress and result commands go.</param>
/// <param name="tasks">Remembers the running work so the shutdown can wait for it; the default is a private set.</param>
public sealed class SyncEffectHandler(
    IRemoteWriter writer,
    IRemoteManager manager,
    IAncestryReader ancestry,
    IDispatcher dispatcher,
    ShutdownTasks? tasks = null)
{
    private const string DeleteNoun = "Remote branch delete";

    private readonly ShutdownTasks _tasks = tasks ?? new ShutdownTasks();

    private readonly ConcurrentDictionary<TabId, CancellationTokenSource> _running = new();

    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(SyncEffect effect, CancellationToken cancellationToken)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _running[effect.TabId] = linked;
        _tasks.Run(() => RunAsync(effect, linked));
    }

    /// <summary>Stops the running effect of the tab; its result is the cancel result. Nothing happens when none runs.</summary>
    public void Cancel(TabId tabId)
    {
        if (_running.TryGetValue(tabId, out var running))
        {
            try
            {
                running.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The effect ended meanwhile.
            }
        }
    }

    private Task RunAsync(SyncEffect effect, CancellationTokenSource linked) =>
        EffectWork.RunAsync(
            token => PerformAsync(effect, token),
            () => new SyncCancelled(effect.TabId),
            exception => Failure(effect, exception),
            result => Finish(effect, linked, result),
            linked.Token);

    // The effect has ended: forget its token source, then post the result.
    private void Finish(SyncEffect effect, CancellationTokenSource linked, SyncCommand result)
    {
        _running.TryRemove(new KeyValuePair<TabId, CancellationTokenSource>(effect.TabId, linked));
        linked.Dispose();
        dispatcher.Post(result);
    }

    private async Task<SyncCommand> PerformAsync(SyncEffect effect, CancellationToken token)
    {
        var progress = new CommandProgress(dispatcher, effect.TabId);
        try
        {
            return await PerformAsync(effect, progress, token).ConfigureAwait(false);
        }
        finally
        {
            progress.Flush();
        }
    }

    private async Task<SyncCommand> PerformAsync(SyncEffect effect, CommandProgress progress, CancellationToken token)
    {
        switch (effect)
        {
            case CheckBehindEffect check:
                var behind = await ancestry
                    .IsAncestorAsync(check.Location, check.CommitHash, check.TrackingHash, token)
                    .ConfigureAwait(false);
                return new SyncBehindChecked(
                    effect.TabId,
                    check.Branch,
                    check.CommitHash,
                    check.Remote,
                    check.TrackingHash,
                    behind);
            case DeleteRemoteBranchEffect delete:
                await writer
                    .DeleteRemoteBranchAsync(delete.Location, delete.Remote, delete.Branch, delete.CommitHash, token)
                    .ConfigureAwait(false);
                var restore = $"git push {delete.Remote} {delete.CommitHash}:{GitRefPrefixes.Heads}{delete.Branch}";
                return new SyncSucceeded(
                    effect.TabId,
                    $"Deleted {delete.Branch} on {delete.Remote} (was {ShortHash.Of(delete.CommitHash)})",
                    UndoNotes.Restore(restore));
            case FetchEffect fetch:
                var request = new FetchRequest(null, fetch.AllRemotes, true);
                await writer.FetchAsync(fetch.Location, request, progress, token).ConfigureAwait(false);
                var fetched = fetch.AllRemotes ? "Fetched all remotes" : "Fetched the remote of the current branch";
                return new SyncSucceeded(effect.TabId, fetched);
            case ListSyncRemotesEffect list:
                var remotes = await manager.ListAsync(list.Location, token).ConfigureAwait(false);
                return new SyncRemotesListed(
                    effect.TabId,
                    list.Purpose,
                    list.Branch,
                    list.DirectName,
                    [.. remotes.Select(remote => remote.Name)]);
            case PullEffect pull:
                var outcome = await writer.PullAsync(pull.Location, progress, token).ConfigureAwait(false);
                var pulled = outcome == PullOutcome.UpToDate ? "Already up to date" : "Pulled (fast-forward)";
                return new SyncSucceeded(effect.TabId, pulled);
            case PushEffect push:
                await writer.PushAsync(push.Location, push.Request, progress, token).ConfigureAwait(false);
                return new SyncSucceeded(effect.TabId, push.Done, push.DoneDetail);
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private static SyncFailed Failure(SyncEffect effect, Exception exception)
    {
        var reason = RemoteFailureText.Reason(exception);
        switch (effect)
        {
            case DeleteRemoteBranchEffect delete:
                var arguments = RemoteCommands.BuildDeleteBranch(
                    delete.Remote,
                    delete.Branch,
                    delete.CommitHash,
                    false);

                return new SyncFailed(
                    effect.TabId,
                    $"Could not delete the branch on the remote: {reason}",
                    RetryOf(exception, DeleteNoun, arguments),
                    null);
            case FetchEffect fetch:
                var fetchArguments = RemoteCommands.BuildFetch(new FetchRequest(null, fetch.AllRemotes, true), false);
                return Transfer(effect, "Fetch", reason, exception, fetchArguments, null);
            case PullEffect:
                return Transfer(effect, "Pull", reason, exception, RemoteCommands.BuildPull(), null);
            case PushEffect push:
                var pushArguments = RemoteCommands.BuildPush(push.Request, false);
                return Transfer(effect, "Push", reason, exception, pushArguments, RejectionOf(push, exception));
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private static SyncFailed Transfer(
        SyncEffect effect,
        string noun,
        string reason,
        Exception exception,
        IReadOnlyList<string> arguments,
        SyncRejection? rejection) =>
        new(effect.TabId, $"{noun} failed: {reason}", RetryOf(exception, noun, arguments), rejection);

    // Only a plain or first push is answered with a force; a push that already was one is not offered another.
    private static SyncRejection? RejectionOf(PushEffect push, Exception exception)
    {
        if (push.Request.Lease is not null || !RemoteFailureText.IsNonFastForward(exception))
        {
            return null;
        }

        return new SyncRejection(push.Branch, push.Request.SetUpstream ? push.Request.Remote : null);
    }

    // The terminal can answer a passphrase, credentials or a host key question that the background run cannot.
    private static SyncRetry? RetryOf(Exception exception, string noun, IReadOnlyList<string> arguments)
    {
        var needsCredentials = exception is GitCommandFailedException failed
            && RemoteAuthFailure.Matches(failed.StandardError);
        return needsCredentials ? new SyncRetry(noun, [.. arguments]) : null;
    }
}
