namespace Gitunia.Git.Tests.Execution;

/// <summary>A throwaway script standing in for git, so tests can simulate slow, failing or chatty processes.</summary>
public sealed class FakeGitExecutable : IDisposable
{
    private readonly string directory;

    private FakeGitExecutable(string directory, string path)
    {
        this.directory = directory;
        Path = path;
    }

    public string Path { get; }

    /// <summary>Creates a script from a body for the current OS: <paramref name="unixBody"/> for sh, <paramref name="windowsBody"/> for cmd.</summary>
    public static FakeGitExecutable Create(string unixBody, string windowsBody)
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gitunia-fake-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path;
        if (OperatingSystem.IsWindows())
        {
            path = System.IO.Path.Combine(directory, "fakegit.cmd");
            File.WriteAllText(path, "@echo off\r\n" + windowsBody + "\r\n");
        }
        else
        {
            path = System.IO.Path.Combine(directory, "fakegit");
            File.WriteAllText(path, "#!/bin/sh\n" + unixBody + "\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return new FakeGitExecutable(directory, path);
    }

    public static FakeGitExecutable Sleeping() =>
        Create("sleep 30", "ping -n 30 127.0.0.1 >nul");

    /// <summary>Records its process id next to the marker (Unix only), prints 'first', sleeps two seconds, then writes the marker file; a killed process never writes it.</summary>
    public static FakeGitExecutable SleepingThenMarking(string markerPath) =>
        Create($"echo $$ > '{markerPath}.pid'\nprintf first\nsleep 2\necho done > '{markerPath}'", $"<nul set /p =\"first\"\r\nping -n 3 127.0.0.1 >nul\r\necho done > \"{markerPath}\"");

    public static FakeGitExecutable EchoingArgumentsAndLocks() =>
        Create("printf '%s\\n' \"$@\"\nprintf 'LOCKS=%s' \"$GIT_OPTIONAL_LOCKS\"", "echo %*\r\n<nul set /p =\"LOCKS=%GIT_OPTIONAL_LOCKS%\"");

    public static FakeGitExecutable Failing(int exitCode, string message) =>
        Create($"echo '{message}' >&2\nexit {exitCode}", $"echo {message} 1>&2\r\nexit /b {exitCode}");

    public static FakeGitExecutable Printing(string text) =>
        Create($"printf '%s' '{text}'", $"<nul set /p =\"{text}\"");

    public static FakeGitExecutable PrintingThenSleeping(string text) =>
        Create($"printf '%s' '{text}'\nsleep 30", $"<nul set /p =\"{text}\"\r\nping -n 30 127.0.0.1 >nul");

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup of a temp directory.
        }
    }
}
