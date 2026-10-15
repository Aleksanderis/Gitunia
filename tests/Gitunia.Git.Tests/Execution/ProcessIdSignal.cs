namespace Gitunia.Git.Tests.Execution;

/// <summary>Completes once a fake git script has recorded its process id next to a marker, so a test can cancel or time out a process that is known to be running.</summary>
public sealed class ProcessIdSignal : IDisposable
{
    private const string ProcessIdSuffix = ".pid";

    private readonly FileSystemWatcher _watcher;
    private readonly string _processIdPath;
    private readonly TaskCompletionSource _recorded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ProcessIdSignal(string markerPath)
    {
        _processIdPath = $"{markerPath}{ProcessIdSuffix}";
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(markerPath)!, $"*{ProcessIdSuffix}");
        _watcher.Created += (_, _) => Check();
        _watcher.Renamed += (_, _) => Check();
        _watcher.Error += (_, _) => Check();
        _watcher.EnableRaisingEvents = true;
        Check();
    }

    public Task Recorded => _recorded.Task;

    /// <summary>Starts watching; call before the process starts.</summary>
    public static ProcessIdSignal Watch(string markerPath) => new(markerPath);

    public void Dispose() => _watcher.Dispose();

    private void Check()
    {
        if (File.Exists(_processIdPath))
        {
            _recorded.TrySetResult();
        }
    }
}
