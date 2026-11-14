using Gitunia.Git.Watching;
using Xunit;

namespace Gitunia.Git.Tests.Watching;

/// <summary>
/// The one way a watcher test waits for watcher state. A wait ends when the state is reached or fails, with the cause,
/// when the watcher gave up watching and keeps polling instead (a watcher that polls never reaches the state, so
/// waiting out the limit only hides why; #1398). Nothing here waits for a wall-clock "long enough".
/// </summary>
public static class WatcherWait
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan PollingGrace = TimeSpan.FromSeconds(2);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Waits until <paramref name="condition"/> holds, without a watcher to watch.</summary>
    public static Task UntilAsync(Func<bool> condition) => UntilAsync(condition, null);

    /// <summary>
    /// Waits until <paramref name="condition"/> holds. With a <paramref name="watcher"/>, fails when it keeps polling
    /// for more than a grace period, unless the test expects that (<paramref name="expectPolling"/>).
    /// </summary>
    public static async Task UntilAsync(
        Func<bool> condition,
        RepositoryWatcher? watcher,
        bool expectPolling = false,
        ChangeRecorder? recorder = null)
    {
        var deadline = DateTime.UtcNow + Limit;
        DateTime? pollingSince = null;
        while (!condition())
        {
            // A fallback can be transient (the watcher retries, an overflow is handled), so only polling that lasts
            // past the grace period fails the wait.
            pollingSince = NextPollingSince(pollingSince, watcher, expectPolling);
            if (pollingSince is { } since && DateTime.UtcNow - since > PollingGrace)
            {
                Assert.Fail(
                    $"The watcher keeps polling, so the awaited state cannot be reached. {Cause()}{Describe(watcher)}");
            }

            Assert.True(DateTime.UtcNow < deadline, $"Condition not reached in time. {Describe(watcher)}{Describe(recorder)}");
            await Task.Delay(PollDelay, Token);
        }
    }

    private static DateTime? NextPollingSince(DateTime? pollingSince, RepositoryWatcher? watcher, bool expectPolling)
    {
        if (watcher is not { IsPolling: true } || expectPolling)
        {
            return null;
        }

        return pollingSince ?? DateTime.UtcNow;
    }

    private static string Cause()
    {
        if (!OperatingSystem.IsLinux())
        {
            return string.Empty;
        }

        return "Usually the per-user inotify instance limit is used up "
            + "(fs.inotify.max_user_instances, shared with every other process of the user). ";
    }

    private static string Describe(ChangeRecorder? recorder) =>
        recorder is null ? string.Empty : $" Recorder: {recorder.CallCount} calls, saw {recorder.Seen}.";

    private static string Describe(RepositoryWatcher? watcher)
    {
        // A starved pool (the timers and the event callbacks run on it) shows as queued work and a thread count far above the CPUs.
        var pool = $"Pool: {ThreadPool.ThreadCount} threads, {ThreadPool.PendingWorkItemCount} queued, "
            + $"{Environment.ProcessorCount} CPUs.";
        if (watcher is null)
        {
            return pool;
        }

        return $"Watcher: polling {watcher.IsPolling}, watchers {watcher.WatcherCount}, "
            + $"directories {watcher.WorkingTreeDirectoryCount}. {pool}";
    }
}
