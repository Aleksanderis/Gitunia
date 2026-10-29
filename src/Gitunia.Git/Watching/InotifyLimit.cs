namespace Gitunia.Git.Watching;

/// <summary>
/// The per-user inotify instance limit on Linux. Every .NET <c>FileSystemWatcher</c> takes one instance, so opt-in
/// watching (one watcher per directory) must stay far below it to leave instances for other programs.
/// </summary>
public static class InotifyLimit
{
    private const string InstancesFile = "/proc/sys/fs/inotify/max_user_instances";
    private const int ShareDivisor = 4;
    private const int MinimumDirectories = 16;

    /// <summary>The limit read from <c>/proc</c>, or null off Linux or when it cannot be read.</summary>
    public static int? ReadMaximumInstances()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            if (int.TryParse(File.ReadAllText(InstancesFile).Trim(), out var instances) && instances > 0)
            {
                return instances;
            }

            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// How many directories one tab may watch: a quarter of the instance limit (at least 16), never above
    /// <paramref name="configured"/>; the configured value stands when the limit is unknown.
    /// </summary>
    public static int DirectoryCap(int? instances, int configured) =>
        instances is { } limit ? Math.Min(configured, Math.Max(MinimumDirectories, limit / ShareDivisor)) : configured;
}
