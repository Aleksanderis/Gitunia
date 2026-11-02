using System.Collections.Concurrent;
using Gitunia.Core.Commits;
using Gitunia.Core.Editing;
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
public sealed class CommitEffectHandler(ICommitWriter writer, IDispatcher dispatcher)
{
    private readonly ConcurrentDictionary<TabId, CancellationTokenSource> _running = new();

    /// <summary>Starts the commit and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(RunCommitEffect effect, CancellationToken cancellationToken)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _running[effect.TabId] = linked;
        _ = Task.Run(() => RunAsync(effect, linked), CancellationToken.None);
    }

    /// <summary>Starts a read of a message and returns at once.</summary>
    public void Handle(CommitReadEffect effect, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => ReadAsync(effect, cancellationToken), CancellationToken.None);
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

    private async Task RunAsync(RunCommitEffect effect, CancellationTokenSource linked)
    {
        CommitCommand result;
        try
        {
            var created = await writer.CommitAsync(effect.Location, effect.Request, linked.Token).ConfigureAwait(false);
            result = new CommitSucceeded(effect.TabId, effect.Request, created, effect.ForcePushUpstream);
        }
        catch (OperationCanceledException)
        {
            result = new CommitFailed(effect.TabId, CommitTexts.CancelledNotice, null);
        }
        catch (GitCommandFailedException exception)
        {
            var reason = CommitFailureReport.Notice(exception.StandardError, exception.StandardOutput);
            var notice = $"Commit failed: {reason}";
            var lines = CommitFailureReport.Lines(exception.StandardError, exception.StandardOutput);
            result = new CommitFailed(effect.TabId, notice, lines);
        }
        catch (Exception exception)
        {
            // Guard of the background run: anything else becomes a failed commit so the box never stays busy.
            var notice = $"Commit failed: {CommitFailureReport.Notice(exception.Message)}";
            result = new CommitFailed(effect.TabId, notice, null);
        }

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
            dispatcher.Post(new HeadMessageReadFailed(effect.TabId, CommitFailureReport.Notice(exception.Message)));
        }
        catch (Exception)
        {
            // Without the prepared merge message the box simply starts empty.
        }
    }
}
