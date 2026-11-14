using System.Diagnostics;

namespace Gitunia.Git.Execution;

/// <summary>
/// Reads one chunk of a process pipe and ends the read when it stays pending too long after the process exited (a
/// background child of a hook can hold the pipe open for ever). The cut only cancels a read that has nothing to
/// deliver.
/// </summary>
/// <remarks>
/// On Windows the pipe is a synchronous <c>FileStream</c>: <c>ReadAsync</c> first hops to the thread pool and checks
/// the token there, before it calls <c>ReadFile</c>. A cancellation that wins against a read still waiting in the pool
/// queue ends the read without looking at the pipe, so output git wrote before it exited is lost (#1340: a starved pool
/// on a loaded runner). The cut is therefore deferred, up to a bound, while the pool has queued work.
/// </remarks>
public sealed class DrainingReader
{
    private static readonly TimeSpan PoolPollInterval = TimeSpan.FromMilliseconds(20);

    // Lets a read that was just taken off the pool queue reach the pipe before the cancellation.
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(10);

    private readonly TimeSpan _grace;
    private readonly TimeSpan _maxDeferral;
    private readonly Func<long> _queuedWorkItems;

    /// <param name="grace">How long a read may stay pending after the exit before it is cut.</param>
    /// <param name="maxDeferral">The longest the cut waits for the thread pool queue to drain (zero: never).</param>
    /// <param name="queuedWorkItems">
    /// The number of queued thread pool work items (a seam for tests; default: the pool's own count).
    /// </param>
    public DrainingReader(TimeSpan grace, TimeSpan maxDeferral, Func<long>? queuedWorkItems = null)
    {
        _grace = grace;
        _maxDeferral = maxDeferral;
        _queuedWorkItems = queuedWorkItems ?? GetPoolQueueLength;
    }

    /// <summary>True when the drain token ended a read and the hard token (timeout, caller cancel) did not.</summary>
    public static bool IsCut(CancellationToken drainToken, CancellationToken hardToken) =>
        drainToken.IsCancellationRequested && !hardToken.IsCancellationRequested;

    /// <summary>
    /// Reads up to <paramref name="buffer"/>.Length bytes. Returns 0 at the end of the stream and when the read was
    /// cut (<paramref name="exitTask"/> completed and the grace passed with nothing to read).
    /// <paramref name="capToken"/> is an absolute bound that cancels a pending read without deferral;
    /// <paramref name="hardToken"/> propagates.
    /// </summary>
    public async Task<int> ReadChunkAsync(
        Stream stream,
        byte[] buffer,
        Task exitTask,
        CancellationToken capToken,
        CancellationToken hardToken)
    {
        using var readSource = CancellationTokenSource.CreateLinkedTokenSource(hardToken, capToken);
        using var stopSource = CancellationTokenSource.CreateLinkedTokenSource(hardToken);

        // The read is issued before the cut is armed, so the cut can only find the read already queued.
        var readTask = stream.ReadAsync(buffer, readSource.Token).AsTask();
        var cutTask = CutAfterExitAsync(exitTask, readSource, stopSource.Token);
        try
        {
            return await readTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (IsCut(readSource.Token, hardToken))
        {
            return 0;
        }
        finally
        {
            await stopSource.CancelAsync().ConfigureAwait(false);
            await cutTask.ConfigureAwait(false);
        }
    }

    private static long GetPoolQueueLength() => ThreadPool.PendingWorkItemCount;

    private async Task CutAfterExitAsync(Task exitTask, CancellationTokenSource readSource, CancellationToken stopToken)
    {
        try
        {
            await exitTask.WaitAsync(stopToken).ConfigureAwait(false);
            await Task.Delay(_grace, stopToken).ConfigureAwait(false);
            await WaitForPoolQueueAsync(stopToken).ConfigureAwait(false);
            await readSource.CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The read ended first.
        }
    }

    // A work item that is no longer queued has been taken by a worker, so a read that hopped through the pool is about
    // to reach the pipe; the settle delay covers the moment between taking it and checking its token. This narrows the
    // race, it does not close it (a worker can be preempted for longer, and the deferral is bounded).
    private async Task WaitForPoolQueueAsync(CancellationToken stopToken)
    {
        var started = Stopwatch.GetTimestamp();
        while (_queuedWorkItems() > 0 && Stopwatch.GetElapsedTime(started) < _maxDeferral)
        {
            await Task.Delay(PoolPollInterval, stopToken).ConfigureAwait(false);
        }

        await Task.Delay(SettleDelay, stopToken).ConfigureAwait(false);
    }
}
