using Gitunia.Core.Keymap;
using System.Collections.Concurrent;
using Gitunia.Core.Commits;
using Gitunia.Core.Editing;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits;
using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.App.Commits;

/// <summary>
/// Runs the commit effects off the UI thread and posts exactly one result command for each: the commit (success, or
/// failure
/// with the output of the hooks, or the cancel notice) and the reads of the messages the box starts from. The cancel
/// of a
/// running commit cancels its own token and the process is killed. It never reads or changes state. Timeouts stay in
/// the
/// Git layer.
/// </summary>
/// <param name="writer">Creates the commit and reads the messages.</param>
/// <param name="dispatcher">Where the result commands go.</param>
/// <param name="keymap">The running keymap; the key tokens of the failure output are resolved against it.</param>
/// <param name="tasks">Remembers the running work so the shutdown can wait for it; the default is a private set.</param>
public sealed class CommitEffectHandler(
    ICommitWriter writer,
    IDispatcher dispatcher,
    KeyBindingTable? keymap = null,
    ShutdownTasks? tasks = null)
{
    private readonly ShutdownTasks _tasks = tasks ?? new ShutdownTasks();
    private readonly ConcurrentDictionary<TabId, CancellationTokenSource> _running = new();

    /// <summary>Starts the commit and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(RunCommitEffect effect, CancellationToken cancellationToken)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _running[effect.TabId] = linked;
        _tasks.Run(() => RunAsync(effect, linked));
    }

    /// <summary>Starts a read of a message and returns at once.</summary>
    public void Handle(CommitReadEffect effect, CancellationToken cancellationToken)
    {
        _tasks.Run(() => ReadAsync(effect, cancellationToken));
    }

    /// <summary>Stops the running commit of the tab; its result is the cancel result. Nothing happens when none
    /// runs.</summary>
    public void Cancel(TabId tabId)
    {
        if (!_running.TryGetValue(tabId, out var running))
        {
            return;
        }

        try
        {
            running.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The commit ended meanwhile.
        }
    }

    private Task RunAsync(RunCommitEffect effect, CancellationTokenSource linked) =>
        EffectWork.RunAsync(
            token => CommitAsync(effect, token),
            () => new CommitFailed(effect.TabId, CommitTexts.CancelledNotice, null),
            exception => Failed(effect, exception),
            result => Finish(effect, linked, result),
            linked.Token);

    private CommitFailed Failed(RunCommitEffect effect, Exception exception)
    {
        if (exception is GitCommandFailedException failed)
        {
            var reason = CommitFailureReport.Notice(failed.StandardError, failed.StandardOutput);
            var lines = CommitFailureReport.Lines(failed.StandardError, failed.StandardOutput)
                .Select(line => KeyTokens.Resolve(line, keymap))
                .ToList();

            return new CommitFailed(effect.TabId, $"Commit failed: {reason}", lines);
        }

        // Guard of the background run: anything else becomes a failed commit so the box never stays busy.
        var notice = $"Commit failed: {CommitFailureReport.Notice(FailureText.Reason(exception))}";

        return new CommitFailed(effect.TabId, notice, null);
    }

    private async Task<CommitCommand> CommitAsync(RunCommitEffect effect, CancellationToken token)
    {
        var created = await writer.CommitAsync(effect.Location, effect.Request, token).ConfigureAwait(false);

        return new CommitSucceeded(effect.TabId, effect.Request, created, effect.ForcePushUpstream);
    }

    // The commit has ended: forget its token source, then post the result.
    private void Finish(RunCommitEffect effect, CancellationTokenSource linked, CommitCommand result)
    {
        _running.TryRemove(new KeyValuePair<TabId, CancellationTokenSource>(effect.TabId, linked));
        linked.Dispose();
        dispatcher.Post(result);
    }

    private async Task ReadAsync(CommitReadEffect effect, CancellationToken token)
    {
        try
        {
            if (effect is ReadHeadMessageEffect head)
            {
                var message = await writer.ReadHeadMessageAsync(head.Location, token).ConfigureAwait(false);
                if (message is not null)
                {
                    dispatcher.Post(new HeadMessageRead(head.TabId, head.Hash, message));
                }

                return;
            }

            var merge = await writer.ReadMergeMessageAsync(effect.Location, token).ConfigureAwait(false);
            if (merge is not null)
            {
                dispatcher.Post(new MergeMessageRead(effect.TabId, merge));
            }
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (Exception exception) when (effect is ReadHeadMessageEffect)
        {
            dispatcher.Post(new HeadMessageReadFailed(effect.TabId, CommitFailureReport.Notice(FailureText.Reason(exception))));
        }
        catch (Exception)
        {
            // Without the prepared merge message the box simply starts empty.
        }
    }
}
