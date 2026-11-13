using Gitunia.Git.Watching;
using Xunit;

namespace Gitunia.Git.Tests.Watching;

/// <summary>
/// The one way a watcher test waits for watcher state. A wait ends when the state is reached or fails at once, with the
/// cause, when the watcher gave up watching and polls instead (a watcher that polls never reaches the state, so
/// waiting out the limit only hides why; #1398). Nothing here waits for a wall-clock "long enough".
/// </summary>
public static class WatcherWait
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(20);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Waits until <paramref name="condition"/> holds, without a watcher to watch.</summary>
    public static Task UntilAsync(Func<bool> condition) => UntilAsync(condition, null);

    /// <summary>
    /// Waits until <paramref name="condition"/> holds. With a <paramref name="watcher"/>, fails as soon as it polls,
    /// unless the test expects that (<paramref name="expectPolling"/>).
    /// </summary>
    public static async Task UntilAsync(Func<bool> condition, RepositoryWatcher? watcher, bool expectPolling = false)
    {
        var deadline = DateTime.UtcNow + Limit;
        while (!condition())
        {
            if (watcher is { IsPolling: true } && !expectPolling)
            {
                Assert.Fail(
                    "The watcher fell back to polling, so the awaited state can never be reached. Usually the "
                    + "per-user inotify instance limit is used up (fs.inotify.max_user_instances, shared with "
                    + $"every other process of the user); watchers: {watcher.WatcherCount}, "
                    + $"directories: {watcher.WorkingTreeDirectoryCount}.");
            }

            Assert.True(DateTime.UtcNow < deadline, $"Condition not reached in time. {Describe(watcher)}");
            await Task.Delay(PollDelay, Token);
        }
    }

    private static string Describe(RepositoryWatcher? watcher)
    {
        if (watcher is null)
        {
            return string.Empty;
        }

        return $"Watcher: polling {watcher.IsPolling}, watchers {watcher.WatcherCount}, "
            + $"directories {watcher.WorkingTreeDirectoryCount}.";
    }
}
